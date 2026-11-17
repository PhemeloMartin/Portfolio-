namespace Portfolio.Web.Infrastructure;

/// <summary>
/// Adds security headers to every response. Headers are added in OnStarting so they survive the
/// exception handler (which clears the response) and so an endpoint can set a stricter policy of its own.
/// </summary>
public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool isDevelopment)
    {
        // Everything (scripts, styles, fonts, images) is served from this site, so the policy can be strict:
        // no inline scripts, no third-party code, no framing by other sites.
        var csp = string.Join("; ",
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self'",
            "img-src 'self' data:",
            "font-src 'self'",
            "connect-src 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'",
            "base-uri 'self'",
            "object-src 'none'",
            isDevelopment ? "" : "upgrade-insecure-requests").TrimEnd(' ', ';');

        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var h = context.Response.Headers;
                h.TryAdd("Content-Security-Policy", csp);
                h.TryAdd("X-Content-Type-Options", "nosniff");
                h.TryAdd("X-Frame-Options", "DENY");
                h.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
                h.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=()");
                h.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
                h.TryAdd("Cross-Origin-Resource-Policy", "same-origin");

                // Keep the admin area out of search engines and shared caches.
                if (context.Request.Path.StartsWithSegments("/admin"))
                {
                    h["X-Robots-Tag"] = "noindex, nofollow";
                    h["Cache-Control"] = "no-store";
                }

                return Task.CompletedTask;
            });

            await next();
        });
    }
}
