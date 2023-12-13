using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace DrumGame.Game.API;

public record OAuthToken
{
    public string AccessToken;
    public string RefreshToken;
    public DateTime Expires;

    public void LoadResponse(TokenResponse response)
    {
        AccessToken = response.access_token;
        RefreshToken = response.refresh_token;
        Expires = DateTime.Now.AddSeconds(response.expires_in).ToUniversalTime();
    }

    public void Save(string path) => File.WriteAllText(path, JsonConvert.SerializeObject(this));

    public record TokenResponse(string access_token, int expires_in, string refresh_token) { }
}

public interface IOAuth<T> where T : IOAuth<T>
{
    public static abstract string TokenFile { get; }
    public static abstract string HttpPrefix { get; } // must match what we have in the host's database
    public static abstract string RedirectUri { get; }
    public static abstract string ClientId { get; }
    public static abstract string TokenUrl { get; }
    public static abstract string AuthCodeUrl { get; }
    public static abstract string ApiBase { get; }

    public static virtual string Scope => null;
    public static virtual string DummyClientSecret => null;
}

// the reason we need this class (and can't just put everything in OAuth) is because we don't want to type out IOAuth<T> when calling static methods
// by inheriting, we can just type `Send` instead of `IOAuth<T>.Send`
public class OAuth<T> where T : IOAuth<T>
{
    public static OAuthToken Token;

    protected OAuth() { } // never make instance, we can't make this class static though due to C# limitations


    static string TokenFilePath => Util.Resources.GetAbsolutePath(T.TokenFile);

    static Task<OAuthToken> Loaded;
    public static async Task<OAuthToken> GetAuth()
    {
        if (Loaded != null && Loaded.IsFaulted) Reset();
        var res = await (Loaded ??= Authorize());
        if (res.Expires < DateTime.Now)
            await TryRefresh(res);
        return res;
    }

    protected static void Reset()
    {
        Loaded = null;
        if (File.Exists(TokenFilePath))
            File.Delete(TokenFilePath);
    }
    protected static async Task TryRefresh(OAuthToken token)
    {
        // here we assume the Token we currently have is invalid, but the RefreshToken is valid
        if (token.RefreshToken == null) throw new Exception();

        var dict = new Dictionary<string, string>();
        dict.Add("grant_type", "refresh_token");
        dict.Add("refresh_token", token.RefreshToken);
        dict.Add("client_id", T.ClientId);
        if (!string.IsNullOrWhiteSpace(T.DummyClientSecret))
            dict.Add("client_secret", T.DummyClientSecret);
        var req = new HttpRequestMessage(HttpMethod.Post, T.TokenUrl) { Content = new FormUrlEncodedContent(dict) };
        token.LoadResponse(await Send<OAuthToken.TokenResponse>(req));
        token.Save(TokenFilePath);
    }
    static async Task<OAuthToken> Authorize()
    {
        var filePath = TokenFilePath;
        if (File.Exists(filePath))
            return JsonConvert.DeserializeObject<OAuthToken>(File.ReadAllText(filePath));

        var res = new OAuthToken();

        var randomCharacters = RandomString(64);
        var sha256 = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(randomCharacters)));
        sha256 = sha256.Replace("+", "-").Replace("/", "_").Replace("=", "");

        // Create a listener.
        using var listener = new HttpListener();
        listener.Prefixes.Add(T.HttpPrefix);
        listener.Start();
        var callback = listener.GetContextAsync();

        var authParameters = $"?client_id={T.ClientId}";
        authParameters += $"&response_type=code&redirect_uri={UrlEncode(T.RedirectUri)}&code_challenge_method=S256";
        authParameters += $"&code_challenge={sha256}";
        var scope = T.Scope;
        if (!string.IsNullOrWhiteSpace(scope))
            authParameters += $"&scope={UrlEncode(scope)}";
        Util.Host.OpenUrlExternally(T.AuthCodeUrl + authParameters);

        var context = await callback;
        var request = context.Request;
        var code = request.QueryString.Get("code");
        var error = request.QueryString.Get("error");
        using (var response = new StreamWriter(context.Response.OutputStream))
        {
            if (error != null)
                response.WriteLine("Authorization failed, please try again.\n" + error);
            else
                response.WriteLine("Authorization successful, you may close this window.");
        }

        var dict = new Dictionary<string, string>();
        dict.Add("grant_type", "authorization_code");
        dict.Add("code", code);
        dict.Add("redirect_uri", T.RedirectUri);
        dict.Add("client_id", T.ClientId);
        dict.Add("code_verifier", randomCharacters);
        if (!string.IsNullOrWhiteSpace(T.DummyClientSecret))
            dict.Add("client_secret", T.DummyClientSecret);
        if (!string.IsNullOrWhiteSpace(scope))
            dict.Add("scope", scope);
        var req = new HttpRequestMessage(HttpMethod.Post, T.TokenUrl) { Content = new FormUrlEncodedContent(dict) };
        var tokenResponse = await Send<OAuthToken.TokenResponse>(req);
        res.LoadResponse(tokenResponse);

        res.Save(TokenFilePath);

        Token = res;

        return res;
    }


    static HttpClient _client;
    static HttpClient Client
    {
        get
        {
            if (_client == null)
            {
                _client = new();
            }
            return _client;
        }
    }

    public static void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }


    protected static async Task<Response> Get<Response>(string endpoint, bool retryOnFail = true)
    {
        var token = await GetAuth();
        try
        {
            var message = new HttpRequestMessage(HttpMethod.Get, T.ApiBase + endpoint);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            return await Send<Response>(message);
        }
        catch (HttpRequestException e) when (retryOnFail && e.StatusCode == HttpStatusCode.Unauthorized)
        {
            await TryRefresh(token);
            return await Get<Response>(endpoint, false);
        }
    }

    protected static async Task<Response> Send<Response>(HttpRequestMessage request)
    {
        using var resp = await Client.SendAsync(request);
        if (!resp.IsSuccessStatusCode)
            Logger.Log(await resp.Content.ReadAsStringAsync(), level: LogLevel.Error);
        resp.EnsureSuccessStatusCode();
        using var content = await resp.Content.ReadAsStreamAsync();

        using var streamReader = new StreamReader(content);
        using var jsonReader = new JsonTextReader(streamReader);

        var serializer = new JsonSerializer();

        return serializer.Deserialize<Response>(jsonReader);
    }

    static string RandomString(int length)
    {
        var o = new char[length];
        var random = new Random();
        const string possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        for (var i = 0; i < length; i++)
            o[i] = possible[random.Next(possible.Length)];
        return new string(o);
    }

    public static string UrlEncode(string s) => HttpUtility.UrlEncode(s);
}