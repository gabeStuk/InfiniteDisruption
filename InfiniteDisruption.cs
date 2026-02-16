using Landfall.Haste;
using Landfall.Modding;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Localization;
using Zorro.Core;
using Zorro.Settings;

namespace InfiniteDisruption
{
    [LandfallPlugin]
    public class InfiniteDisruption
    {
        private static bool fragModOn = false;
        private static readonly List<FragmentModifier> fragMods = [];
        private static readonly System.Random rnd = new();
        private static readonly List<string> boosts =
        [
            "BoostBonus",
            "BoostIncrease"
        ];
        private static readonly Type[] itemTypes = [
            typeof(FragmentModifierEffect_ChooseFromThreeItems),
            typeof(FragmentModifierEffect_GetSingleRandomItem)
        ];

        private static readonly Action StartRunHook = () =>
        {
            Debug.Log("[ID] Run Started");
            if (RunHandler.config.isEndless && !RunHandler.RunData.runConfigRuntimeData.DisableFragmentModifiers)
            {
                fragModOn = true;

                // disable actual fragment disruption
                RunHandler.config.FragmentModifierBaseChance = 0.0f;
                RunHandler.config.FragmentModifierSecondaryChance = 0.0f;

                typeof(GM_API).GetEvent("NewLevel").AddEventHandler(null, NewLevelHook);
                Debug.Log("[ID] Bound hook: 'NewLevel'");
            }
        };

        private static readonly Action<RunHandler.LastRunState> EndRunHook = lastState =>
        {
            if (fragModOn)
            {
                typeof(GM_API).GetEvent("NewLevel").RemoveEventHandler(null, NewLevelHook);
                Debug.Log($"[ID] Unhooked 'NewLevel'");
            }
        };

        private static void PrintFMList(FragmentModifierSet fms)
        {
            foreach (var item in fms.FragmentModifiers)
            {
                Debug.Log($"[ID] {item.FragmentModifier} (weight: {item.Weight})");
            }
        }

        private static readonly Action NewLevelHook = () =>
        {

            Debug.Log($"[ID] New Level Started -- affected? {(fragModOn ? "true" : "false")}");
            if (!fragModOn) return;
            var skipNotify = false;
            var chance = GameHandler.Instance.SettingsHandler.GetSetting<IDPercentChance>().Value;
            if (rnd.NextDouble() * 100 > chance)
            {
                Debug.Log("[ID] No Fragment Modifier");
                skipNotify = true;
                goto apply; // skip adding a new fragment but still apply current
            }
            var src = GameHandler.Instance.SettingsHandler.GetSetting<IDUseAll>().Value ? FragmentModifierDatabase.Instance.DefaultFragmentModifierSet : RunHandler.config.FragmentModifierSet;
            Debug.Log("[ID] List: ");
            PrintFMList(src);
            FragmentModifier fm;
            if (GameHandler.Instance.SettingsHandler.GetSetting<IDOnlyBoost>().Value)
            { // BoostBonus BoostIncrease
                fm = GameHandler.Instance.SettingsHandler.GetSetting<IDUseWeights>().Value ? src.GetRandomFragmentModifier(rnd, [.. src.FragmentModifiers.Where((fm) => !boosts.Any((b) => fm.FragmentModifier.name.Contains(b))).Select((fm) => fm.FragmentModifier)]) : src.FragmentModifiers.Where((fm) => boosts.Any((b) => fm.FragmentModifier.name.Contains(b))).ToList().GetRandom().FragmentModifier;
            }
            else
            {
                fm = GameHandler.Instance.SettingsHandler.GetSetting<IDUseWeights>().Value ? src.GetRandomFragmentModifier(rnd, []) : src.FragmentModifiers[rnd.Next(src.FragmentModifiers.Length)].FragmentModifier;
            }
            if ((!GameHandler.Instance.SettingsHandler.GetSetting<IDRepeatItems>().Value) && fm.Effects.Length > 0 && fm.Effects.Any((e) => itemTypes.Contains(e.GetType())))
            {
                Debug.Log($"[ID] Skip append and apply {fm}");
                NotificationHandler.Instance.AddFragmentModifierNotification(fm);
                fm.ExecuteFragmentModifierEffect();
                goto apply;
            }
            Debug.Log($"[ID] Appending FM: {fm}");
            fragMods.Add(fm);
        apply:
            ApplyModifiers(skipNotify);
        };

        static InfiniteDisruption()
        {
            typeof(GM_API).GetEvent("StartNewRun").AddEventHandler(null, StartRunHook);
            Debug.Log("[ID] Bound hook: 'StartNewRun'");
            typeof(GM_API).GetEvent("RunEnd").AddEventHandler(null, EndRunHook);
            Debug.Log("[ID] Bound hook: 'RunEnd'");
        }

        private static void ApplyModifiers(bool skipNotify)
        {
            var showAll = GameHandler.Instance.SettingsHandler.GetSetting<IDShowAll>().Value;
            foreach (var modifier in fragMods)
            {
                Debug.Log($"[ID] Applying Modifier: {modifier}");
                if (showAll)
                    NotificationHandler.Instance.AddFragmentModifierNotification(modifier);
                modifier.ExecuteFragmentModifierEffect();
            }
            if (!showAll && !skipNotify) NotificationHandler.Instance.AddFragmentModifierNotification(fragMods.Last());
        }
    }

    [HasteSetting]
    public class IDPercentChance : FloatSetting, IExposedSetting
    {
        public override void ApplyValue() => Debug.Log($"[ID] {GetType().Name} => {Value}");
        protected override float GetDefaultValue() => 40;
        protected override float2 GetMinMaxValue() => new(0, 100);
        public LocalizedString GetDisplayName() => new UnlocalizedString("Chance for fragment modifier (%)");
        public string GetCategory() => "InfiniteDisruption";
    }

    [HasteSetting]
    public class IDRepeatItems : BoolSetting, IExposedSetting
    {
        public override void ApplyValue() => Debug.Log($"[ID] {GetType().Name} => {Value}");
        protected override bool GetDefaultValue() => true;
        public LocalizedString GetDisplayName() => new UnlocalizedString("Repeat item-giving modifiers");
        public string GetCategory() => "InfiniteDisruption";

        public override LocalizedString OnString => new UnlocalizedString("Repeat");
        public override LocalizedString OffString => new UnlocalizedString("Do Not Repeat");

        
    }

    [HasteSetting]
    public class IDShowAll : BoolSetting, IExposedSetting
    {
        public override void ApplyValue() => Debug.Log($"[ID] {GetType().Name} => {Value}");
        protected override bool GetDefaultValue() => true;
        public LocalizedString GetDisplayName() => new UnlocalizedString("Show notifications for all modifiers");
        public string GetCategory() => "InfiniteDisruption";
        public override LocalizedString OnString => new UnlocalizedString("Show");
        public override LocalizedString OffString => new UnlocalizedString("Show only last");
    }

    [HasteSetting]
    public class IDUseWeights : BoolSetting, IExposedSetting
    {
        public override void ApplyValue() => Debug.Log($"[ID] {GetType().Name} => {Value}");
        protected override bool GetDefaultValue() => true;
        public LocalizedString GetDisplayName() => new UnlocalizedString("Use the default weighted modifier selection");
        public string GetCategory() => "InfiniteDisruption";
        public override LocalizedString OnString => new UnlocalizedString("Weighted");
        public override LocalizedString OffString => new UnlocalizedString("Unweighted");
    }

    [HasteSetting]
    public class IDUseAll : BoolSetting, IExposedSetting
    {
        public override void ApplyValue() => Debug.Log($"[ID] {GetType().Name} => {Value}");
        protected override bool GetDefaultValue() => true;
        public LocalizedString GetDisplayName() => new UnlocalizedString("Select from full list of modifiers, even those not avaliable in endless (effects may not function)");
        public string GetCategory() => "InfiniteDisruption";
        public override LocalizedString OnString => new UnlocalizedString("All");
        public override LocalizedString OffString => new UnlocalizedString("Endless Only");
    }

    [HasteSetting]
    public class IDOnlyBoost : BoolSetting, IExposedSetting
    {
        public override void ApplyValue() => Debug.Log($"[ID] {GetType().Name} => {Value}");
        protected override bool GetDefaultValue() => true;
        public LocalizedString GetDisplayName() => new UnlocalizedString("Only possible fragment modifier is a boost doubler");
        public string GetCategory() => "InfiniteDisruption";
        public override LocalizedString OnString => new UnlocalizedString("Only Boost");
        public override LocalizedString OffString => new UnlocalizedString("Everything");
    }
}