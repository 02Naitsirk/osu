// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Online.Notifications.WebSocket;
using osu.Game.Online.Notifications.WebSocket.Events;
using osu.Game.Users;

namespace osu.Game.Overlays
{
    public partial class MedalOverlay : OsuFocusedOverlayContainer
    {
        protected override string? PopInSampleName => null;
        protected override string? PopOutSampleName => null;

        public override bool IsPresent => base.IsPresent || Scheduler.HasPendingTasks;

        protected override void PopIn() => this.FadeIn();

        protected override void PopOut() => this.FadeOut();

        private readonly Queue<MedalAnimation> queuedMedals = new Queue<MedalAnimation>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private Container<Drawable> medalContainer = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            api.NotificationsClient.MessageReceived += handleMedalMessages;

            Add(medalContainer = new Container
            {
                RelativeSizeAxes = Axes.Both
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            OverlayActivationMode.BindValueChanged(val =>
            {
                if (val.NewValue == OverlayActivation.All && (queuedMedals.Any() || medalContainer.Any()))
                    Show();
            }, true);
        }

        private void handleMedalMessages(SocketMessage obj)
        {
            if (obj.Event != @"new")
                return;

            var data = obj.Data?.ToObject<NewPrivateNotificationEvent>();
            if (data == null || data.Name != @"user_achievement_unlock")
                return;

            var details = data.Details?.ToObject<UserAchievementUnlock>();
            if (details == null)
                return;

            var medal = new Medal
            {
                Name = details.Title,
                InternalName = details.Slug,
                Description = details.Description,
            };

            var medalAnimation = new MedalAnimation(medal);
            queuedMedals.Enqueue(medalAnimation);
            if (OverlayActivationMode.Value == OverlayActivation.All)
                Scheduler.AddOnce(Show);
        }

        protected override void Update()
        {
            base.Update();

            if (medalContainer.Any())
                return;

            if (!queuedMedals.TryDequeue(out var medal))
            {
                Hide();
                return;
            }

            LoadComponentAsync(medal, medalContainer.Add);
        }

        protected override bool OnClick(ClickEvent e)
        {
            (medalContainer.FirstOrDefault(anim => anim.IsAlive) as MedalAnimation)?.Dismiss();
            return true;
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Back)
            {
                (medalContainer.FirstOrDefault(anim => anim.IsAlive) as MedalAnimation)?.Dismiss();
                return true;
            }

            return base.OnPressed(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (api.IsNotNull())
                api.NotificationsClient.MessageReceived -= handleMedalMessages;
        }
    }
}
