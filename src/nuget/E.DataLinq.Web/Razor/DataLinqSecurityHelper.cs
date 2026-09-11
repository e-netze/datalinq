using E.DataLinq.Core;
using E.DataLinq.Core.Reflection;
using E.DataLinq.Core.Services.KeyValueStore;
using E.DataLinq.Core.Services.KeyValueStore.Abstraction;
using E.DataLinq.Web.Html;
using E.DataLinq.Web.Html.Abstractions;
using E.DataLinq.Web.Services;
using E.DataLinq.Web.Services.Abstraction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Razor;

/// <summary>
/// de: Die Klasse ist eine Hilfsklasse für sicherheitsbezogene Funktionen, die innerhalb der Razor-Umgebung von DataLinq genutzt werden kann. Der Zugriff erfolgt über den globalen Namen DataLinqSecurityHelper bzw. der Kurzform SECURITY.
/// en: This class is a helper class for security related functions that can be used within the Razor environment of DataLinq. It is accessed through the global name DataLinqSecurityHelper or the shorthand SECURITY.
/// </summary>
public class DataLinqSecurityHelper
{
    private readonly HttpContext _httpContext;
    private readonly IDataLinqUser _ui;
    private readonly IRazorCompileEngineService _razor;

    public DataLinqSecurityHelper(
        HttpContext httpContext,
        IDataLinqUser ui,
        IRazorCompileEngineService razor = null)
    {
        _httpContext = httpContext;
        _ui = ui;
        _razor = razor;
    }

    /// <summary>
    /// de: Der Username des aktuell angemeldeten Benutzers.
    /// en: The username of the currently logged-in user.
    /// </summary>
    /// <returns>
    /// de: Gibt den Username des aktuell angemeldeten Benutzers zurück, falls verfügbar, ansonsten einen leeren String.
    /// en: Returns the username of the currently logged-in user, if available, otherwise an empty string.
    /// </returns>
    public string GetCurrentUsername()
        => _ui?.Username ?? "";

    /// <summary>
    /// de: Prüft, ob der aktuelle User Mitglied in der angegeben Rolle ist.
    /// en: Checks if the current user is a member of the specified role.
    /// </summary>
    /// <param name="roleName">
    /// de: Der Name der Rolle, die geprüft werden soll.
    /// en: The name of the role to be checked.
    /// </param>
    /// <returns>
    /// de: Gibt true zurück, wenn der Benutzer Mitglied der angegebenen Rolle ist, andernfalls false.
    /// en: Returns true if the user is a member of the specified role, otherwise false.
    /// </returns>
    public bool HasRole(
            string roleName
        ) => _ui?
             .Userroles?
             .Any(r => r.Equals(roleName, StringComparison.InvariantCultureIgnoreCase)) == true;

    /// <summary>
    /// de: Liefert den Wert eines Rollenparameters zurück, zB GKZ (Gemeindekennzahl). Rollenparameter werden nur in speziellen Authentication Umgebungen wie PVP unterstützt.
    /// en: Returns the value of a role parameter, e.g., GKZ (municipality code). Role parameters are only supported in specific authentication environments like PVP.
    /// </summary>
    /// <param name="claimName">
    /// de: Der Name des Claims, dessen Wert zurückgegeben werden soll.
    /// en: The name of the claim whose value is to be returned.
    /// </param>
    /// /// <param name="caseSensitiv">
    /// de: Gibt an, ob bei der Suche nach dem Claim auf Groß-/Kleinschreibung geachtet werden soll (true) oder nicht (false). Standardwert ist true.
    /// en: Indicates whether the search for the claim should be case-sensitive (true) or not (false). The default value is true.
    /// </param>
    /// <returns>
    /// de: Gibt den Wert des angegebenen Claims zurück, falls vorhanden, ansonsten einen leeren String.
    /// en: Returns the value of the specified claim, if available, otherwise an empty string.
    /// </returns>
    public string GetUserClaim(
        string claimName,
        bool caseSensitiv = true)
    {
        var claimValue = _ui?
            .Claims?
            .Where(p => p?.StartsWith($"{claimName}=", caseSensitiv ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase) == true)
            .FirstOrDefault()?
            .Substring(claimName.Length + 1)
            .Trim() ?? "";

        return claimValue;
    }

    /// <summary>
    /// de: Gibt den Wert eines HTTP Request Headers zurück.
    /// en: Returns the value of an HTTP request header.
    /// </summary>
    /// <param name="header">
    /// de: Der Name des Headers, dessen Wert zurückgegeben werden soll.
    /// en: The name of the header whose value is to be returned.
    /// </param>
    /// <returns>
    /// de: Gibt den Wert des angegebenen Headers zurück. Falls der Header nicht vorhanden ist, wird ein leerer String zurückgegeben.
    /// en: Returns the value of the specified header. If the header is not present, an empty string is returned.
    /// </returns>
    public string GetRequestHeaderValue(string header)
        => _httpContext?.Request?.Headers[header] ?? "";

    /// <summary>
    /// de: Liest einen verschlüsselten Wert (Secret) aus dem globalen Secret-Store (secrets.blb) und gibt den entschlüsselten Wert zurück. Secrets können über die DataLinq Code Oberfläche verwaltet werden.
    /// en: Reads an encrypted value (secret) from the global secret store (secrets.blb) and returns the decrypted value. Secrets can be managed through the DataLinq Code UI.
    /// </summary>
    /// <param name="key">
    /// de: Der Schlüssel (Name) des Secrets, dessen Wert zurückgegeben werden soll.
    /// en: The key (name) of the secret whose value should be returned.
    /// </param>
    /// <returns>
    /// de: Gibt den entschlüsselten Wert des Secrets zurück, falls vorhanden, ansonsten einen leeren String.
    /// en: Returns the decrypted value of the secret, if available, otherwise an empty string.
    /// </returns>
    public string GetSecret(string key)
        => GetKeyValueStore()?.GetValue(KeyValueStoreType.Secret, key) ?? "";

    /// <summary>
    /// de: Berechnet den HMAC-SHA256 einer Nachricht mit einem geheimen Schlüssel und gibt das Ergebnis als hexadezimalen String (Kleinbuchstaben) zurück.
    /// en: Computes the HMAC-SHA256 of a message using a secret key and returns the result as a lower-case hexadecimal string.
    /// </summary>
    /// <param name="message">
    /// de: Die Nachricht (der zu signierende Text), für die der HMAC-SHA256 berechnet werden soll.
    /// en: The message (the text to be signed) for which the HMAC-SHA256 should be computed.
    /// </param>
    /// <param name="key">
    /// de: Der geheime Schlüssel, der zur Berechnung des HMAC verwendet wird.
    /// en: The secret key used to compute the HMAC.
    /// </param>
    /// <returns>
    /// de: Gibt den berechneten HMAC-SHA256 als hexadezimalen String (Kleinbuchstaben) zurück.
    /// en: Returns the computed HMAC-SHA256 as a lower-case hexadecimal string.
    /// </returns>
    public string HMAC256(
        string message,
        string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// de: Ruft ein ArcGIS Server Bild ab und gibt es als HTML img-Element mit der angegebenen Größe zurück. Der Diensttyp bestimmt, ob ein MapServer- oder ImageServer-Endpunkt aufgerufen wird. Falls ein Token-Objekt mit Anmeldedaten übergeben wird, wird zunächst ein Token angefordert und die Anfrage abgesichert.
    /// en: Retrieves an ArcGIS Server image and returns it as an HTML img element with the specified size. The service type determines whether a MapServer or ImageServer endpoint is called. If a token object with credentials is supplied, a token is requested first and the request is secured.
    /// </summary>
    /// <param name="serviceType">
    /// de: Der Diensttyp, der bestimmt, ob ein MapServer- oder ImageServer-Endpunkt in der URL verwendet wird.
    /// en: The service type that determines whether a MapServer or ImageServer endpoint is used in the URL.
    /// </param>
    /// <param name="serverKey">
    /// de: Die Basis-URL des ArcGIS Servers. Der Wert wird direkt verwendet und nicht mehr aufgelöst. Um die URL nicht direkt anzugeben, kann sie im Razor-Code über GetSecret oder GetConstant aufgelöst werden.
    /// en: The base URL of the ArcGIS Server. The value is used directly and is no longer resolved. To avoid specifying the URL directly, resolve it in Razor code via GetSecret or GetConstant.
    /// </param>
    /// <param name="serviceName">
    /// de: Der Name (Pfad) des ArcGIS Dienstes, der abgerufen werden soll.
    /// en: The name (path) of the ArcGIS service to be retrieved.
    /// </param>
    /// <param name="parameters">
    /// de: Ein dynamisches Objekt mit beliebigen Key-Value-Paaren, die an die URL angehängt werden. size wird für die Größe des img-Elements verwendet. Alle weiteren Parameter (z.B. bbox, format, transparent, layers, show) werden als Query-Parameter übergeben.
    /// en: A dynamic object with arbitrary key-value pairs appended to the URL. size is used for the img element size. All other parameters (e.g., bbox, format, transparent, layers, show) are passed as query parameters.
    /// </param>
    /// <param name="token">
    /// de: Das optionale Token-Objekt mit UsernameKey und PasswordKey. UsernameKey und PasswordKey müssen Schlüssel (Namen) von Secrets sein - Konstanten oder direkte Werte sind nicht erlaubt. Wird nur bei abgesicherten Diensten benötigt.
    /// en: The optional token object containing UsernameKey and PasswordKey. UsernameKey and PasswordKey must be keys (names) of secrets - constants or direct values are not allowed. Only required for secured services.
    /// </param>
    /// <param name="htmlAttributes">
    /// de: Ein optionales Objekt mit HTML-Attributen, die dem img-Element hinzugefügt werden.
    /// en: An optional object containing HTML attributes to be added to the img element.
    /// </param>
    /// <returns>
    /// de: Gibt das abgerufene Bild als HTML img-Element zurück.
    /// en: Returns the retrieved image as an HTML img element.
    /// </returns>
    public async Task<object> GetAgsImage(
        AgsServiceType serviceType,
        string serverKey,
        string serviceName,
        object parameters,
        AgsTokenObject token,
        object htmlAttributes = null)
    {
        var values = ToParameterDictionary(parameters);

        var agsToken = await ResolveAgsToken(serverKey, token);
        var dataUri = await GetAgsServiceImage(serviceType, serverKey, serviceName, values, agsToken);

        return BuildImageTag(dataUri, htmlAttributes);
    }

    #region Helpers
    [ExcludeFromSnippets]
    private object BuildImageTag(string dataUri, object htmlAttributes)
    {
        var htmlBuilder = HtmlBuilder.Create()
            .Append("img", img =>
            {
                img.AddAttribute("src", dataUri);
                img.AddAttributes(htmlAttributes);
            }, WriteTags.SelfClose);

        return _razor is not null
            ? _razor.RawString(htmlBuilder.BuildHtmlString())
            : htmlBuilder.BuildHtmlString();
    }

    [ExcludeFromSnippets]
    private static IDictionary<string, string> ToParameterDictionary(object parameters)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (parameters is not null)
        {
            foreach (var property in parameters.GetType().GetProperties())
            {
                values[property.Name] = property.GetValue(parameters)?.ToString() ?? "";
            }
        }

        return values;
    }

    [ExcludeFromSnippets]
    private async Task<string> ResolveAgsToken(string serverKey, AgsTokenObject token)
    {
        if (token is null
            || string.IsNullOrWhiteSpace(token.UsernameKey)
            || string.IsNullOrWhiteSpace(token.PasswordKey))
        {
            return "";
        }

        var username = ResolveSecret(token.UsernameKey);
        var password = ResolveSecret(token.PasswordKey);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Username and password must be provided as keys of secrets. Constants or direct values are not allowed.");
        }

        return await GetAgsToken(serverKey, username, password);
    }

    [ExcludeFromSnippets]
    private async Task<string> GetAgsToken(
        string server,
        string username,
        string password)
    {
        var baseUrl = ResolveServerBaseUrl(server);
        var requestUri = $"{baseUrl}/tokens/generateToken";

        EnsureImageRequestAllowed(requestUri);

        using var httpClient = new HttpClient();

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("request", "gettoken"),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("expiration", "1"),
            new KeyValuePair<string, string>("f", "json")
        });

        var response = await httpClient.PostAsync(requestUri, content);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();

        using var jsonDocument = JsonDocument.Parse(responseBody);
        var root = jsonDocument.RootElement;

        return root.TryGetProperty("token", out var token)
            ? token.GetString() ?? ""
            : "";
    }

    [ExcludeFromSnippets]
    private async Task<string> GetAgsServiceImage(
        AgsServiceType serviceType,
        string serverKey,
        string serviceName,
        IDictionary<string, string> parameters,
        string token = null)
    {
        var baseUrl = ResolveServerBaseUrl(serverKey);

        var (serverFolder, operation) = serviceType switch
        {
            AgsServiceType.MapService => ("MapServer", "export"),
            AgsServiceType.ImageService => ("ImageServer", "exportImage"),
            _ => throw new ArgumentOutOfRangeException(nameof(serviceType))
        };

        var query = string.Join("&", parameters
            .Where(p => !string.Equals(p.Key, "serverKey", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(p.Key, "serviceName", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.Key}={p.Value}"));

        var requestUri = $"{baseUrl}/rest/services/{serviceName}/{serverFolder}/{operation}?{query}&f=image";

        if (!string.IsNullOrWhiteSpace(token))
            requestUri += $"&token={token}";

        EnsureImageRequestAllowed(requestUri);

        using var httpClient = new HttpClient();

        var response = await httpClient.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (mediaType is null || !mediaType.StartsWith("image/"))
            throw new InvalidOperationException("The response is not an image.");

        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
        return $"data:{mediaType};base64,{Convert.ToBase64String(imageBytes)}";
    }

    [ExcludeFromSnippets]
    private string ResolveServerBaseUrl(string serverBaseUrl)
        => serverBaseUrl?.TrimEnd('/');

    [ExcludeFromSnippets]
    private string ResolveSecret(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            return null;

        var value = GetKeyValueStore()?.GetValue(KeyValueStoreType.Secret, secretKey);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [ExcludeFromSnippets]
    private IKeyValueStoreService GetKeyValueStore()
        => _httpContext?.RequestServices?.GetService<IKeyValueStoreService>();

    [ExcludeFromSnippets]
    private void EnsureImageRequestAllowed(string requestUri)
    {
        var options = _httpContext?.RequestServices?.GetService<IOptions<DataLinqOptions>>()?.Value;

        if (options is null || !options.IsImageRequestUrlAllowed(requestUri))
        {
            throw new InvalidOperationException(
                $"The request url is not allowed. Add its base url to the image request whitelist via the api startup configuration (DataLinqOptions.AddToImageRequestWhiteList).");
        }
    }
    #endregion
}

public enum AgsServiceType
{
    MapService,
    ImageService
}

public class AgsTokenObject
{
    public string UsernameKey { get; set; }

    public string PasswordKey { get; set; }
}
