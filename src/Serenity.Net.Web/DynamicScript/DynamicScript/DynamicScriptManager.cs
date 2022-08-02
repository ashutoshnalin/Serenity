using Microsoft.Extensions.Options;

namespace Serenity.Web
{
    public partial class DynamicScriptManager : IDynamicScriptManager
    {
        private readonly ConcurrentDictionary<string, IDynamicScript> registeredScripts;
        private readonly ConcurrentDictionary<string, DateTime> scriptLastChange;
        private Action<string> scriptChanged;

        private readonly ITwoLevelCache cache;
        private readonly IPermissionService permissions;
        private readonly ITextLocalizer localizer;
        private readonly ILocalTextRegistry textRegistry;
        private readonly IOptions<LocalTextPackages> localTextPackages;
        private static readonly UTF8Encoding utf8Encoding = new(true);

        public DynamicScriptManager(ITwoLevelCache cache, IPermissionService permissions, ITextLocalizer localizer, ILocalTextRegistry textRegistry, IOptions<LocalTextPackages> localTextPackages = null)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
            this.localizer = localizer;
            this.textRegistry = textRegistry ?? throw new ArgumentNullException(nameof(textRegistry));
            this.localTextPackages = localTextPackages;
            registeredScripts = new ConcurrentDictionary<string, IDynamicScript>(StringComparer.OrdinalIgnoreCase);
            scriptLastChange = new ConcurrentDictionary<string, DateTime>();
            Register(new RegisteredScripts(this));
        }

        static readonly HashSet<string> KnownCultureIdentifiers = new(StringComparer.OrdinalIgnoreCase)
        {
            "de",
            "de-DE",
            "en",
            "en-GB",
            "en-UK",
            "en-US",
            "es",
            "es-ES",
            "fa-IR",
            "fr",
            "it-IT",
            "pt-BR",
            "pt-PT",
            "ru",
            "ru-RU",
            "tr",
            "tr-TR",
            "vi-VN",
            "zh-CN"
        };

        public bool IsRegistered(string name)
        {
            return registeredScripts.ContainsKey(name);
        }

        public void Changed(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            scriptLastChange[name] = DateTime.UtcNow;
            scriptChanged?.Invoke(name);
        }

        public void IfNotRegistered(string name, Func<IDynamicScript> callback)
        {
            registeredScripts.GetOrAdd(name, (name) => callback());
        }

        public void Register(INamedDynamicScript script)
        {
            Register(script.ScriptName, script);
        }

        public void Register(string name, IDynamicScript script)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            registeredScripts[name] = script ?? throw new ArgumentNullException(nameof(script));
        }

        public Dictionary<string, string> GetRegisteredScripts()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in registeredScripts)
            {
                var key = s.Key;
                if (key != "RegisteredScripts")
                {
                    result[key] = PeekScriptHash(key, s.Value);
                }
            }
            return result;
        }

        public IEnumerable<string> GetRegisteredScriptNames()
        {
            return registeredScripts.Keys;
        }

        public string PeekScriptHash(string name, IDynamicScript script)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            var cacheKey = "DynamicScript:" + name;
            if (script is ICacheSuffix ics)
                cacheKey = cacheKey + ":" + ics.CacheSuffix;

            var groupKey = script.GroupKey;

            ScriptContent scriptContent;
            if (groupKey == null)
                scriptContent = cache.Memory.Get<ScriptContent>(cacheKey, TimeSpan.Zero, null);
            else
                scriptContent = cache.GetLocalStoreOnly<ScriptContent>(cacheKey, TimeSpan.Zero, groupKey, null);

            if (scriptContent != null &&
                scriptLastChange.TryGetValue(name, out DateTime lastChange) &&
                lastChange >= scriptContent.Time)
            {
                if (groupKey == null)
                    cache.Memory.Remove(cacheKey);
                else
                    cache.Remove(cacheKey);

                scriptContent = null;
            }

            var hash = scriptContent?.Hash;
            if (hash == null)
            {
                hash = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
                if (script is ICacheSuffix ics2)
                    hash = "-" + ics2.CacheSuffix.GetHashCode(StringComparison.Ordinal).ToString(CultureInfo.InvariantCulture);
            }

            return hash;
        }

        private IScriptContent EnsureScriptContent(string name, IDynamicScript script)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            var cacheKey = "DynamicScript:" + name;
            if (script is ICacheSuffix ics)
                cacheKey = cacheKey + ":" + ics.CacheSuffix;

            ScriptContent factory()
            {
                var content = utf8Encoding.GetBytes(script.GetScript());
                return new ScriptContent(content, DateTime.UtcNow, content.Length > 4096);
            }

            var groupKey = script.GroupKey;

            ScriptContent getOrCreate()
            {
                if (groupKey == null)
                    return cache.Memory.Get(cacheKey, script.Expiration, factory);
                else
                    return cache.GetLocalStoreOnly(cacheKey, script.Expiration, groupKey, factory);
            };

            var scriptContent = getOrCreate();
            if (scriptLastChange.TryGetValue(name, out DateTime lastChange) &&
                lastChange >= scriptContent.Time)
            {
                if (groupKey == null)
                    cache.Memory.Remove(cacheKey);
                else
                    cache.Remove(cacheKey);

                return getOrCreate();
            }

            return scriptContent;
        }

        public void Reset()
        {
            foreach (var name in registeredScripts.Keys)
                scriptLastChange[name] = DateTime.UtcNow;
        }

        public void CheckScriptRights(string name)
        {
            if (GetRegisteredScriptByName(name, out var script))
                script.CheckRights(permissions, localizer);
        }

        public string GetScriptText(string name)
        {
            if (!GetRegisteredScriptByName(name, out var script))
                return null;

            var content = EnsureScriptContent(name, script).Content;
            return utf8Encoding.GetString(content);
        }

        public string GetScriptInclude(string name, string extension = ".js")
        {
            if (!GetRegisteredScriptByName(name, out var script))
                return name;

            var hash = PeekScriptHash(name, script);

            return name + extension + "?v=" + hash;
        }

        private bool GetRegisteredScriptByName(string name, out IDynamicScript script)
        {
            if (registeredScripts.TryGetValue(name, out script))
                return true;

            if (!name.StartsWith("LocalText.", StringComparison.OrdinalIgnoreCase))
                return false;

            var parts = name.Split('.');
            if (parts.Length == 4 &&
                localTextPackages?.Value.TryGetValue(parts[1], out var includes) == true &&
                (KnownCultureIdentifiers.Contains(parts[2]) ||
                    CultureInfo.GetCultures(CultureTypes.AllCultures).Any(x => string.Equals(x.Name, parts[2], StringComparison.OrdinalIgnoreCase))))
            {
                IfNotRegistered(name, () =>
                {
                    return new LocalTextScript(textRegistry, parts[1], includes, parts[2], string.Equals(parts[3], "Pending", StringComparison.OrdinalIgnoreCase));
                });

                if (registeredScripts.TryGetValue(name, out script))
                    return true;
            }

            return false;
        }

        public IScriptContent ReadScriptContent(string name)
        {
            if (!GetRegisteredScriptByName(name, out var script))
                return null;

            script.CheckRights(permissions, localizer);

            return EnsureScriptContent(name, script);
        }

        public event Action<string> ScriptChanged
        {
            add
            {
                lock (registeredScripts)
                    scriptChanged += value;
            }
            remove
            {
                lock (registeredScripts)
                    scriptChanged -= value;
            }
        }
    }
}