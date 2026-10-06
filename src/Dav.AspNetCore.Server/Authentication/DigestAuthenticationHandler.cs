using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dav.AspNetCore.Server.Authentication;

internal class DigestAuthenticationHandler : AuthenticationHandler<DigestAuthenticationSchemeOptions>
{
    public DigestAuthenticationHandler(
        IOptionsMonitor<DigestAuthenticationSchemeOptions> options, 
        ILoggerFactory logger, 
        UrlEncoder encoder) 
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationValues))
            return AuthenticateResult.NoResult();

        var authorizationHeader = authorizationValues.ToString();
        if (!authorizationHeader.StartsWith("Digest ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var parameters = ParseParameters(authorizationHeader.Substring("Digest ".Length));

        if (!parameters.TryGetValue("username", out var userName) ||
            !parameters.TryGetValue("realm", out var realm) ||
            !parameters.TryGetValue("nonce", out var nonce) ||
            !parameters.TryGetValue("uri", out var uri) ||
            !parameters.TryGetValue("response", out var clientResponse) ||
            !parameters.TryGetValue("opaque", out var opaque))
        {
            return AuthenticateResult.NoResult();
        }

        // Validate the realm so a response produced for another realm cannot be replayed here.
        var expectedRealm = Options.Realm ?? Context.Request.Host.ToString();
        if (!string.Equals(realm, expectedRealm, StringComparison.Ordinal))
            return AuthenticateResult.Fail("Invalid realm.");

        // Validate the request uri so a response cannot be reused for a different resource.
        if (!UriMatchesRequest(uri))
            return AuthenticateResult.Fail("The digest uri does not match the request.");

        parameters.TryGetValue("qop", out var qop);
        parameters.TryGetValue("nc", out var nonceCount);
        parameters.TryGetValue("cnonce", out var clientNonce);

        var hasQop = !string.IsNullOrWhiteSpace(qop);
        var parsedNonceCount = 0L;
        if (hasQop)
        {
            // With qop=auth the client must provide nc and cnonce (RFC 7616), otherwise the
            // request is not replay protected.
            if (string.IsNullOrWhiteSpace(nonceCount) || string.IsNullOrWhiteSpace(clientNonce))
                return AuthenticateResult.Fail("Missing nonce count or client nonce.");

            if (!long.TryParse(nonceCount, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsedNonceCount))
                return AuthenticateResult.Fail("Invalid nonce count.");
        }

        if (!DigestNonceStore.TryValidate(nonce, opaque, hasQop, parsedNonceCount, out var nonceError))
            return AuthenticateResult.Fail(nonceError ?? "Invalid nonce.");

        if (Options.Events.OnPasswordRequested == null)
            return AuthenticateResult.NoResult();

        var digestContext = new DigestPasswordRequestedContext(
            Context,
            Options,
            Scheme,
            userName);

        var password = await Options.Events.OnPasswordRequested(digestContext, Context.RequestAborted);
        if (string.IsNullOrWhiteSpace(password))
            return AuthenticateResult.NoResult();

        var algorithm = ResolveAlgorithm();
        var ha1 = ComputeHash(algorithm, $"{userName}:{realm}:{password}");
        var ha2 = ComputeHash(algorithm, $"{Context.Request.Method}:{uri}");

        string response;
        if (!hasQop)
        {
            response = ComputeHash(algorithm, $"{ha1}:{nonce}:{ha2}");
        }
        else if (qop!.Equals("auth", StringComparison.OrdinalIgnoreCase))
        {
            response = ComputeHash(algorithm, $"{ha1}:{nonce}:{nonceCount}:{clientNonce}:{qop}:{ha2}");
        }
        else
        {
            return AuthenticateResult.Fail("Unsupported quality of protection parameter.");
        }

        if (!FixedTimeEquals(response, clientResponse.ToLowerInvariant()))
            return AuthenticateResult.NoResult();

        var identity = new Identity(
            DigestAuthenticationDefaults.AuthenticationScheme,
            true,
            userName);

        var claimsIdentity = new ClaimsIdentity(identity);
        var authenticatedContext = new AuthenticatedContext<DigestAuthenticationSchemeOptions>(
            Context,
            Options,
            Scheme,
            claimsIdentity);

        if (Options.Events.OnAuthenticated != null)
            await Options.Events.OnAuthenticated(authenticatedContext, Context.RequestAborted);

        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        return AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var (nonce, opaque) = DigestNonceStore.Create();
        var realm = Options.Realm ?? Context.Request.Host.ToString();
        var algorithm = ResolveAlgorithm();

        // The realm is sanitized so it cannot break out of the header value.
        Context.Response.Headers["WWW-Authenticate"] =
            $"Digest realm=\"{SanitizeHeaderValue(realm)}\", " +
            "qop=\"auth\", " +
            $"nonce=\"{nonce}\", " +
            $"opaque=\"{opaque}\", " +
            $"algorithm=\"{algorithm}\", " +
            "charset=\"UTF-8\"";

        return base.HandleChallengeAsync(properties);
    }

    private string ResolveAlgorithm()
        => string.Equals(Options.Algorithm, "SHA-256", StringComparison.OrdinalIgnoreCase)
            ? "SHA-256"
            : "MD5";

    private static string ComputeHash(string algorithm, string input)
    {
        using HashAlgorithm hashAlgorithm = algorithm == "SHA-256"
            ? SHA256.Create()
            : MD5.Create();

        var hash = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private bool UriMatchesRequest(string clientUri)
    {
        var candidate = clientUri;
        if (Uri.TryCreate(clientUri, UriKind.Absolute, out var absolute))
            candidate = absolute.PathAndQuery;

        var pathBase = Context.Request.PathBase.ToUriComponent();
        var path = Context.Request.Path.ToUriComponent();
        var query = Context.Request.QueryString.ToUriComponent();

        var accepted = new HashSet<string>(StringComparer.Ordinal)
        {
            pathBase + path + query,
            pathBase + path,
            path + query,
            path
        };

        foreach (var value in accepted.ToArray())
            accepted.Add(Uri.UnescapeDataString(value));

        return accepted.Contains(candidate) || accepted.Contains(Uri.UnescapeDataString(candidate));
    }

    private static string SanitizeHeaderValue(string value)
        => value.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

    /// <summary>
    /// Parses the digest parameters. Malformed or duplicated entries are ignored instead of
    /// throwing, so a malformed Authorization header cannot turn into an unhandled exception.
    /// </summary>
    private static Dictionary<string, string> ParseParameters(string input)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var index = 0;
        while (index < input.Length)
        {
            while (index < input.Length && (input[index] == ',' || char.IsWhiteSpace(input[index])))
                index++;

            var keyStart = index;
            while (index < input.Length && input[index] != '=')
                index++;

            if (index >= input.Length)
                break;

            var key = input.Substring(keyStart, index - keyStart).Trim();
            index++; // skip '='

            string value;
            if (index < input.Length && input[index] == '"')
            {
                index++;
                var valueBuilder = new StringBuilder();
                while (index < input.Length && input[index] != '"')
                {
                    if (input[index] == '\\' && index + 1 < input.Length)
                        index++;

                    valueBuilder.Append(input[index]);
                    index++;
                }

                value = valueBuilder.ToString();
                if (index < input.Length)
                    index++; // skip closing quote
            }
            else
            {
                var valueStart = index;
                while (index < input.Length && input[index] != ',')
                    index++;

                value = input.Substring(valueStart, index - valueStart).Trim();
            }

            if (key.Length > 0)
                parameters[key] = value;
        }

        return parameters;
    }

    private record Identity(string? AuthenticationType, bool IsAuthenticated, string? Name) : IIdentity;
}
