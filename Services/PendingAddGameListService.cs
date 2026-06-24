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

        public bool HasDraft => _draft != null;

        public GameConfig GetDraft() => _draft;

        public bool IsPendingGame(GameConfig game)
        {
            if (game == null || _draft == null)
                return false;
            return game.GameGuid != Guid.Empty && game.GameGuid == _draft.GameGuid;
        }

        public void SetDraft(GameConfig game)
        {
            _draft = game;
        }

        public void Clear()
        {
            _draft = null;
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
