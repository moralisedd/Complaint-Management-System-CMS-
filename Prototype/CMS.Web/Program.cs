using CMS.Application.Complaints;
using CMS.Application.Support;
using CMS.Domain.Interfaces;
using CMS.Domain.Strategies;
using CMS.Infrastructure.Data;
using CMS.Infrastructure.Middleware;
using CMS.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// SERVICES
// =============================================================================

builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Needed by TenantSchemaInterceptor so it can read the current request's tenant
// without being a scoped service itself (it's registered as singleton).
builder.Services.AddHttpContextAccessor();

// Swagger — useful for manually testing API endpoints during development.
// I added the Bearer security definition so I can paste a Keycloak token and test directly.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CMS API", Version = "v1" });

    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Description = "Enter 'Bearer {token}' — get a token from Keycloak at http://localhost:8080",
        In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type        = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        Reference   = new Microsoft.OpenApi.Models.OpenApiReference
        {
            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
            Id   = "Bearer"
        }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// EF Core + PostgreSQL.
// I register TenantSchemaInterceptor as a singleton so it's shared across contexts,
// but it reads the tenant ID from the HTTP request scope via IHttpContextAccessor.
builder.Services.AddSingleton<TenantSchemaInterceptor>();
builder.Services.AddDbContext<CmsDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
    options.AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
});

// TenantContext is scoped so each HTTP request gets its own instance.
// I register both the concrete type and the interface so middleware can set it
// and services can read it without depending on the concrete class.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// Strategy pattern — both strategies are registered so the factory can pick
// the right one based on the tenant's industry at runtime.
builder.Services.AddSingleton<IResolutionStrategy, BankResolutionStrategy>();
builder.Services.AddSingleton<IResolutionStrategy, TelecomResolutionStrategy>();
builder.Services.AddSingleton<ResolutionStrategyFactory>();

// Repositories and services — scoped so they share the same DbContext per request.
builder.Services.AddScoped<IComplaintRepository, EfComplaintRepository>();
builder.Services.AddScoped<ISupportPersonRepository, EfSupportPersonRepository>();
builder.Services.AddScoped<ITenantIndustryLookup, TenantIndustryLookup>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
builder.Services.AddScoped<ISupportAssignmentService, SupportAssignmentService>();

// =============================================================================
// AUTHENTICATION — two Keycloak realms, one per tenant
// =============================================================================
// I register two named OIDC schemes so NatWest and O2 users each sign in through
// their own Keycloak realm. The /login page lets the user choose which organisation
// they belong to, then issues a Challenge for the right scheme.
//
// I removed DefaultChallengeScheme so unauthenticated requests fall back to the
// cookie scheme, which redirects to /login rather than jumping straight to Keycloak.
var kc = builder.Configuration.GetSection("Keycloak");

// Shared config for both schemes — authority URL is the only difference between them.
Action<OpenIdConnectOptions, string> ConfigureOidc = (options, authority) =>
{
    options.Authority             = authority;
    options.ClientId              = kc["ClientId"];
    options.ClientSecret          = kc["ClientSecret"];
    options.ResponseType          = OpenIdConnectResponseType.Code;
    options.SaveTokens            = bool.Parse(kc["SaveTokens"] ?? "true");
    options.RequireHttpsMetadata  = bool.Parse(kc["RequireHttpsMetadata"] ?? "true");
    options.GetClaimsFromUserInfoEndpoint = true;

    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    // Without this, ASP.NET Core renames 'role' to a long WCF URI and the
    // authorization policies never match.
    options.MapInboundClaims = false;

    // The 'tid' and 'role' claims come from the ID token, not the userinfo endpoint,
    // so I copy them into the ClaimsPrincipal manually here.
    options.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = ctx =>
        {
            var identity    = ctx.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
            var tokenClaims = ctx.SecurityToken?.Claims ?? [];

            if (identity is null) return Task.CompletedTask;

            foreach (var claimType in new[] { "tid", "role" })
            {
                var value = tokenClaims.FirstOrDefault(c => c.Type == claimType)?.Value;
                if (value is not null && !identity.HasClaim(claimType, value))
                    identity.AddClaim(new System.Security.Claims.Claim(claimType, value));
            }
            return Task.CompletedTask;
        }
    };
};

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath        = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan   = TimeSpan.FromHours(8);

    // If the request carries a Bearer token, hand off to the JWT Bearer scheme.
    // This lets the same auth pipeline serve both the Razor Pages UI (cookies)
    // and the REST API (Bearer tokens) without changing the controllers.
    options.ForwardDefaultSelector = ctx =>
        ctx.Request.Headers.ContainsKey("Authorization") &&
        ctx.Request.Headers["Authorization"].ToString().StartsWith("Bearer ")
            ? JwtBearerDefaults.AuthenticationScheme
            : null;

    // For /api/* paths, return 401/403 directly instead of redirecting to /login.
    // Redirecting to an HTML login page is the wrong response for an API client.
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
})
.AddOpenIdConnect("natwest-oidc", options =>
{
    ConfigureOidc(options, kc["NatWestAuthority"] ?? "http://localhost:8080/realms/natwest-dev");
    // Each scheme needs its own callback path. If both share /signin-oidc, ASP.NET Core
    // can't tell which handler should process the Keycloak response and throws
    // "Unable to unprotect the message.State".
    options.CallbackPath        = "/signin-natwest";
    options.SignedOutCallbackPath = "/signout-callback-natwest";
})
.AddOpenIdConnect("o2-oidc", options =>
{
    ConfigureOidc(options, kc["O2Authority"] ?? "http://localhost:8080/realms/o2-dev");
    options.CallbackPath        = "/signin-o2";
    options.SignedOutCallbackPath = "/signout-callback-o2";
})
// JWT Bearer — validates Keycloak access tokens sent by REST API clients (e.g. Newman, Swagger).
// ForwardDefaultSelector on the Cookie scheme routes Bearer requests here automatically.
// We accept tokens from either realm; since both are on the same Keycloak instance we use
// the NatWest realm authority. ValidateAudience is off because Keycloak doesn't add an 'aud'
// claim for the client_credentials / ROPC flows used in tests.
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Authority            = kc["NatWestAuthority"] ?? "http://localhost:8080/realms/natwest-dev";
    options.RequireHttpsMetadata = false;
    options.MapInboundClaims     = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = false,
        NameClaimType    = "preferred_username",
        RoleClaimType    = "role"
    };
});

// =============================================================================
// AUTHORISATION — role-based policies per use case
// =============================================================================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanLogComplaint",    p => p.RequireAuthenticatedUser().RequireClaim("role", "Consumer"));
    options.AddPolicy("CanAssignSupport",   p => p.RequireAuthenticatedUser().RequireClaim("role", "HelpDeskAgent"));
    options.AddPolicy("CanRecordResolution",p => p.RequireAuthenticatedUser().RequireClaim("role", "SupportPerson"));
    options.AddPolicy("CanViewKpiDashboard",p => p.RequireAuthenticatedUser().RequireClaim("role", "TenantAdmin"));
    options.AddPolicy("CanOnboardTenant",   p => p.RequireAuthenticatedUser().RequireClaim("role", "PlatformAdmin"));

    // Any route not covered by an explicit policy still requires a logged-in user.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// =============================================================================
// PIPELINE
// =============================================================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1"));
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Order matters: auth must run before the tenant middleware so the JWT
// is validated before I try to read the 'tid' claim from it.
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolverMiddleware>();

app.MapRazorPages();
app.MapControllers();

// Logout — signs the user out of the ASP.NET cookie and then redirects to
// Keycloak's end_session endpoint to kill the SSO session too.
// I derive the right OIDC scheme from the 'tid' claim so the correct realm is called.
app.MapGet("/logout", async (HttpContext ctx) =>
{
    var tid        = ctx.User.FindFirst("tid")?.Value;
    var oidcScheme = tid == "o2" ? "o2-oidc" : "natwest-oidc";

    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(oidcScheme, new AuthenticationProperties { RedirectUri = "/" });
}).AllowAnonymous();

app.Run();
