using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopGravity
{
    public class ShopGravity : BasePlugin
    {
        public override string ModuleName => "[SHOP] Gravity";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.1";

        private IShopApi? SHOP_API;
        private const string CategoryName = "Gravity";
        public static JObject? JsonGravity { get; private set; }
        private readonly PlayerGravity[] playerGravity = new PlayerGravity[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/Gravity.json");
            if (File.Exists(configPath))
            {
                JsonGravity = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonGravity == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Гравитация");

            var sortedItems = JsonGravity
                .Properties()
                .Select(p => new { Key = p.Name, Value = (JObject)p.Value })
                .OrderBy(p => (int)p.Value["gravity"]!)
                .ToList();

            foreach (var item in sortedItems)
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(item.Key, (string)item.Value["name"]!, CategoryName, (int)item.Value["price"]!, (int)item.Value["sellprice"]!, (int)item.Value["duration"]!);
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerGravity[playerSlot] = null!);
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            if (TryGetItemGravity(uniqueName, out float gravity))
            {
                playerGravity[player.Slot] = new PlayerGravity(gravity, itemId);
            }
            else
            {
                Logger.LogError($"{uniqueName} has invalid or missing 'lvl' in config!");
            }
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1 && TryGetItemGravity(uniqueName, out float gravity))
            {
                playerGravity[player.Slot] = new PlayerGravity(gravity, itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerGravity[player.Slot] = null!;
            if (player.Pawn.Value != null)
            {
                player.Pawn.Value.GravityScale = 1.0f;
            }
            return HookResult.Continue;
        }

        private static bool TryGetItemGravity(string uniqueName, out float gravity)
        {
            gravity = 0;
            if (JsonGravity != null && JsonGravity.TryGetValue(uniqueName, out var obj) && obj is JObject jsonItem && jsonItem["gravity"] != null && jsonItem["gravity"]!.Type != JTokenType.Null)
            {
                gravity = float.Parse(jsonItem["gravity"]!.ToString());
                return true;
            }
            return false;
        }

        public record class PlayerGravity(float Gravity, int ItemID);
    }
}