using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using CorgiPlay.AddressablesTools;
using UnityEngine;
using UnityEngine.Networking;
using CorgiPlay.Unity.Core.Diagnostics;

namespace CorgiPlay.PuzzleGame.App
{
    public static class AddressablesWrapper
    {
        private static readonly object baseUrlSyncRoot = new object();
        private static string cachedBaseUrlRegionCode;
        private static string cachedBaseUrl;

        // Referenced by Addressables ProfileDataSourceSettings as a static profile variable.
        public static string BASE_URL =>
            GetBaseUrlForCurrentRegion(
                AddressablesRemotePathConfig.DefaultEuropeUrl,
                AddressablesRemotePathConfig.DefaultRussiaBelarusUrl);

        private static string GetBaseUrlForCurrentRegion(
            string europeUrl,
            string russiaBelarusUrl)
        {
            string regionCode = AddressablesRemotePathRouter.GetCurrentRegionCode();

            lock (baseUrlSyncRoot)
            {
                if (cachedBaseUrlRegionCode == regionCode &&
                    !string.IsNullOrWhiteSpace(cachedBaseUrl))
                {
                    return cachedBaseUrl;
                }
            }

            string primaryUrl = AddressablesRemotePathRouter.IsEuropeRegionCode(regionCode)
                ? europeUrl
                : russiaBelarusUrl;

            string normalizedBaseUrl = AddressablesRemotePathUtility.NormalizeUrl(primaryUrl);

            lock (baseUrlSyncRoot)
            {
                cachedBaseUrlRegionCode = regionCode;
                cachedBaseUrl = normalizedBaseUrl;
            }

            return normalizedBaseUrl;
        }
    }

    public static class AddressablesRemotePathRouter
    {
        private const string RussiaRegionCode  = "RU";
        private const string BelarusRegionCode = "BY";
        private const int IpGeolocationTimeoutSeconds = 4;

        private static readonly object regionSyncRoot = new object();
        private static string runtimeRegionCode;
        private static string runtimeRegionSource;
        private static string runtimeRegionProvider;
        private static readonly IpGeolocationEndpoint[] IpGeolocationEndpoints =
        {
            new IpGeolocationEndpoint(
                "ipapi.co",
                "https://ipapi.co/country/",
                IpGeolocationResponseFormat.PlainCountryCode),
            new IpGeolocationEndpoint(
                "country.is",
                "https://api.country.is/",
                IpGeolocationResponseFormat.CountryIsJson),
            new IpGeolocationEndpoint(
                "ipwhois.app",
                "https://ipwhois.app/json/",
                IpGeolocationResponseFormat.IpWhoisJson)
        };

        public static async UniTask ApplyAsync(CancellationToken cancellationToken)
        {
            string regionCode = await ResolveRegionCodeAsync(cancellationToken);
            ConfigureFallback(regionCode);
        }

        public static bool IsEurope() =>
            IsEuropeRegionCode(GetCurrentRegionCode());

        public static string GetCurrentRegionCode()
        {
            lock (regionSyncRoot)
            {
                if (!string.IsNullOrWhiteSpace(runtimeRegionCode))
                {
                    return runtimeRegionCode;
                }
            }

            string cultureRegionCode = GetCultureRegionCodeOrUnknown();
            SetRuntimeRegion(
                cultureRegionCode,
                "culture",
                string.Empty);
            return cultureRegionCode;
        }

        public static string GetCurrentRegionGroup() =>
            IsEuropeRegionCode(GetCurrentRegionCode()) ? "Europe" : "RussiaBelarus";

        public static string GetCurrentRegionSource() =>
            GetRuntimeRegionSource();

        public static string GetCurrentRegionProvider() =>
            GetRuntimeRegionProvider();

        private static void ConfigureFallback(string regionCode)
        {
            bool isEurope = IsEuropeRegionCode(regionCode);
            string primaryUrl = AddressablesRemotePathUtility.NormalizeUrl(
                isEurope
                    ? AddressablesRemotePathConfig.DefaultEuropeUrl
                    : AddressablesRemotePathConfig.DefaultRussiaBelarusUrl);
            string fallbackUrl = AddressablesRemotePathUtility.NormalizeUrl(
                isEurope
                    ? AddressablesRemotePathConfig.DefaultRussiaBelarusUrl
                    : AddressablesRemotePathConfig.DefaultEuropeUrl);

            AddressablesRemotePathFallback.Configure(
                primaryUrl,
                fallbackUrl);
        }

        private static async UniTask<string> ResolveRegionCodeAsync(
            CancellationToken cancellationToken)
        {
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                IpGeolocationResult ipGeolocationResult =
                    await TryGetIpRegionAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(ipGeolocationResult.RegionCode))
                {
                    SetRuntimeRegion(
                        ipGeolocationResult.RegionCode,
                        "ip",
                        ipGeolocationResult.Provider);

                    AppLogger.Debug(
                        $"[AddressablesRemotePath] Region from IP geolocation: {ipGeolocationResult.RegionCode}, " +
                        $"provider={ipGeolocationResult.Provider}");
                    return ipGeolocationResult.RegionCode;
                }
            }

            string cultureRegionCode = GetCultureRegionCodeOrUnknown();
            SetRuntimeRegion(
                cultureRegionCode,
                "culture",
                string.Empty);
            AppLogger.Debug($"[AddressablesRemotePath] Region from CultureInfo fallback: {cultureRegionCode}");
            return cultureRegionCode;
        }

        private static string GetRegionCodeFromCulture(CultureInfo cultureInfo)
        {
            if (cultureInfo == null || cultureInfo.IsNeutralCulture)
                return null;

            try
            {
                return new RegionInfo(cultureInfo.Name)
                    .TwoLetterISORegionName
                    .ToUpperInvariant();
            }
            catch
            {
                return null;
            }
        }

        private static string GetCultureRegionCodeOrUnknown() =>
            GetCultureRegionCodeOrNull() ?? "UNKNOWN";

        private static string GetCultureRegionCodeOrNull() =>
            GetRegionCodeFromCulture(CultureInfo.CurrentCulture) ??
            GetRegionCodeFromCulture(CultureInfo.CurrentUICulture);

        private static async UniTask<IpGeolocationResult> TryGetIpRegionAsync(
            CancellationToken cancellationToken)
        {
            foreach (IpGeolocationEndpoint endpoint in IpGeolocationEndpoints)
            {
                try
                {
                    using UnityWebRequest request = UnityWebRequest.Get(endpoint.Url);
                    request.timeout = IpGeolocationTimeoutSeconds;

                    await request.SendWebRequest()
                        .ToUniTask(cancellationToken: cancellationToken);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        AppLogger.Warning(
                            $"[AddressablesRemotePath] IP geolocation failed. " +
                            $"provider={endpoint.Provider}, result={request.result}, error={request.error}");
                        continue;
                    }

                    string regionCode = ParseIpGeolocationRegionCode(
                        endpoint.ResponseFormat,
                        request.downloadHandler.text);

                    if (string.IsNullOrWhiteSpace(regionCode))
                    {
                        AppLogger.Warning(
                            $"[AddressablesRemotePath] IP geolocation returned empty country. " +
                            $"provider={endpoint.Provider}");
                        continue;
                    }

                    return new IpGeolocationResult(regionCode, endpoint.Provider);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    AppLogger.Warning(
                        $"[AddressablesRemotePath] IP geolocation exception. " +
                        $"provider={endpoint.Provider}, error={exception.Message}");
                }
            }

            return default;
        }

        private static string ParseIpGeolocationRegionCode(
            IpGeolocationResponseFormat responseFormat,
            string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return null;
            }

            switch (responseFormat)
            {
                case IpGeolocationResponseFormat.CountryIsJson:
                    return ParseCountryIsRegionCode(responseText);
                case IpGeolocationResponseFormat.IpWhoisJson:
                    return ParseIpWhoisRegionCode(responseText);
                case IpGeolocationResponseFormat.PlainCountryCode:
                    return NormalizeRegionCode(responseText);
                default:
                    return null;
            }
        }

        private static string ParseCountryIsRegionCode(string json)
        {
            try
            {
                CountryIsResponse response = JsonUtility.FromJson<CountryIsResponse>(json);
                return NormalizeRegionCode(response?.country);
            }
            catch
            {
                return null;
            }
        }

        private static string ParseIpWhoisRegionCode(string json)
        {
            try
            {
                IpWhoisResponse response = JsonUtility.FromJson<IpWhoisResponse>(json);
                return NormalizeRegionCode(response?.country_code);
            }
            catch
            {
                return null;
            }
        }

        public static bool IsEuropeRegionCode(string regionCode)
        {
            string normalizedRegionCode = NormalizeRegionCode(regionCode);

            if (normalizedRegionCode == null)
            {
                return true;
            }

            return normalizedRegionCode != RussiaRegionCode &&
                   normalizedRegionCode != BelarusRegionCode;
        }

        private static void SetRuntimeRegion(
            string regionCode,
            string source,
            string provider)
        {
            lock (regionSyncRoot)
            {
                runtimeRegionProvider = provider ?? string.Empty;
                runtimeRegionSource = source ?? "unknown";
                runtimeRegionCode = regionCode;
            }
        }

        private static string GetRuntimeRegionSource()
        {
            lock (regionSyncRoot)
            {
                return string.IsNullOrWhiteSpace(runtimeRegionSource)
                    ? "unknown"
                    : runtimeRegionSource;
            }
        }

        private static string GetRuntimeRegionProvider()
        {
            lock (regionSyncRoot)
            {
                return string.IsNullOrWhiteSpace(runtimeRegionProvider)
                    ? string.Empty
                    : runtimeRegionProvider;
            }
        }

        private static string NormalizeRegionCode(string regionCode)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return null;
            }

            string normalizedRegionCode = regionCode.Trim().ToUpperInvariant();

            if (normalizedRegionCode.Length != 2 ||
                normalizedRegionCode[0] < 'A' ||
                normalizedRegionCode[0] > 'Z' ||
                normalizedRegionCode[1] < 'A' ||
                normalizedRegionCode[1] > 'Z')
            {
                return null;
            }

            return normalizedRegionCode;
        }

        private readonly struct IpGeolocationEndpoint
        {
            public IpGeolocationEndpoint(
                string provider,
                string url,
                IpGeolocationResponseFormat responseFormat)
            {
                Provider = provider;
                Url = url;
                ResponseFormat = responseFormat;
            }

            public string Provider { get; }
            public string Url { get; }
            public IpGeolocationResponseFormat ResponseFormat { get; }
        }

        private readonly struct IpGeolocationResult
        {
            public IpGeolocationResult(string regionCode, string provider)
            {
                RegionCode = regionCode;
                Provider = provider;
            }

            public string RegionCode { get; }
            public string Provider { get; }
        }

        private enum IpGeolocationResponseFormat
        {
            CountryIsJson,
            PlainCountryCode,
            IpWhoisJson
        }

        [Serializable]
        private sealed class CountryIsResponse
        {
            // JsonUtility deserializes public fields, not properties.
            public string country;
        }

        [Serializable]
        private sealed class IpWhoisResponse
        {
            public string country_code;
        }
    }
}
