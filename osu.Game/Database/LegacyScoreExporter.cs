// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;

namespace osu.Game.Database
{
    public class LegacyScoreExporter : LegacyModelExporter<ScoreInfo>
    {
        public LegacyScoreExporter(Storage storage, RealmAccess realm)
            : base(storage, realm)
        {
        }

        protected override string FileExtension => ".osr";

        protected override void ExportToStream(ScoreInfo model, Stream stream, ProgressNotification notification)
        {
            var file = model.Files.SingleOrDefault();
            if (file == null)
                return;

            using (var inputStream = UserFileStorage.GetStream(file.File.GetStoragePath()))
                inputStream.CopyTo(stream);
        }
    }
}
