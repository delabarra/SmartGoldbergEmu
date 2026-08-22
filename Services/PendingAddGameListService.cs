using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // In-memory add-game draft shown in the list during collect/preview; not persisted to games.ini until save.
    public sealed class PendingAddGameListService
    {
        private GameConfig _draft;
        private bool _isUpdateDraft;

        public bool HasDraft => _draft != null;

        public bool IsUpdateDraft => _isUpdateDraft && _draft != null;

        public GameConfig GetDraft() => _draft;

        public bool IsPendingGame(GameConfig game)
        {
            if (game == null || _draft == null)
                return false;
            return game.GameGuid != Guid.Empty && game.GameGuid == _draft.GameGuid;
        }

        public bool IsPendingUpdate(GameConfig game)
        {
            return IsUpdateDraft && IsPendingGame(game);
        }

        public void SetDraft(GameConfig game, bool isUpdate = false)
        {
            _draft = game;
            _isUpdateDraft = isUpdate && game != null;
        }

        public void Clear()
        {
            _draft = null;
            _isUpdateDraft = false;
        }

        public static GameConfig CreateDraftFromExecutable(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new ArgumentException("Executable path is required.", nameof(executablePath));

            string fullPath = Path.GetFullPath(executablePath.Trim());
            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "New game";

            string startFolder = Path.GetDirectoryName(fullPath) ?? string.Empty;

            return new GameConfig
            {
                GameGuid = Guid.NewGuid(),
                AppName = fileName,
                AppId = 0,
                Path = fullPath,
                StartFolder = startFolder,
                WorkingDirectory = startFolder,
                Parameters = string.Empty
            };
        }

        // Same library GUID so SyncPendingAdd updates the existing list row instead of appending a second one.
        public static GameConfig CreateUpdateDraft(GameConfig existing)
        {
            if (existing == null)
                throw new ArgumentNullException(nameof(existing));

            return new GameConfig
            {
                GameGuid = existing.GameGuid,
                AppName = existing.AppName ?? string.Empty,
                AppId = existing.AppId,
                Path = existing.Path ?? string.Empty,
                StartFolder = existing.StartFolder ?? string.Empty,
                WorkingDirectory = existing.WorkingDirectory ?? string.Empty,
                Parameters = existing.Parameters ?? string.Empty,
                CustomIcon = existing.CustomIcon ?? string.Empty,
                LaunchMode = existing.LaunchMode
            };
        }

        // Keeps the draft GameGuid when collect refreshes identity fields.
        public void ApplyCollectedGame(GameConfig collected)
        {
            if (collected == null)
                return;

            if (_draft == null)
            {
                _draft = collected;
                return;
            }

            Guid draftGuid = _draft.GameGuid;
            _draft = collected;
            if (draftGuid != Guid.Empty)
                _draft.GameGuid = draftGuid;
        }

        public List<GameConfig> MergeInto(IReadOnlyList<GameConfig> persistedGames)
        {
            if (_draft == null)
                return persistedGames == null ? new List<GameConfig>() : new List<GameConfig>(persistedGames);

            var merged = persistedGames == null ? new List<GameConfig>() : new List<GameConfig>(persistedGames);
            if (merged.Any(g => g != null && g.GameGuid == _draft.GameGuid))
                return merged;

            merged.Add(_draft);
            return merged;
        }
    }
}
