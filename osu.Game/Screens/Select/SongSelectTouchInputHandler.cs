// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Select
{
    public partial class SongSelectTouchInputHandler : Component
    {
        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        private IBindable<bool> touchActive = null!;

        [BackgroundDependencyLoader]
        private void load(SessionStatics statics)
        {
            touchActive = statics.GetBindable<bool>(Static.TouchInputActive);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateState());
            mods.BindValueChanged(_ => updateState());
            touchActive.BindValueChanged(_ => updateState());
            updateState();
        }

        private void updateState()
        {
            var touchDeviceMod = ruleset.Value.CreateInstance().GetTouchDeviceMod();

            if (touchDeviceMod == null)
                return;

            bool touchDeviceModEnabled = mods.Value.Any(mod => mod is ModTouchDevice);

            if (touchActive.Value && !touchDeviceModEnabled)
                mods.Value = mods.Value.Append(touchDeviceMod).ToArray();
            if (!touchActive.Value && touchDeviceModEnabled)
                mods.Value = mods.Value.Where(mod => mod is not ModTouchDevice).ToArray();
        }
    }
}
