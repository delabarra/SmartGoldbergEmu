using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class MainForm : Form
    {
        private void SchedulePersistDetailsColumnWidths()
        {
            if (IsDisposed || Disposing)
                return;
            if (_detailsColumnWidthsSaveTimer == null)
            {
                _detailsColumnWidthsSaveTimer = new Timer { Interval = DetailsColumnWidthsSaveDebounceMs };
                _detailsColumnWidthsSaveTimer.Tick += DetailsColumnWidthsSaveTimer_Tick;
            }

            _detailsColumnWidthsSaveTimer.Stop();
            _detailsColumnWidthsSaveTimer.Start();
        }

        private void DetailsColumnWidthsSaveTimer_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                _detailsColumnWidthsSaveTimer?.Stop();
                return;
            }
            _detailsColumnWidthsSaveTimer.Stop();
            FlushPersistDetailsColumnWidths();
        }

        private void FlushPersistDetailsColumnWidths()
        {
            _detailsColumnWidthsSaveTimer?.Stop();
            if (_appDataService.GetViewMode() != ApplicationConstants.ViewModeDetails)
                return;

            ListViewColumnHelper.PersistDetailsColumnWidthsIfChanged(
                lstGames,
                _appDataService,
                ApplicationConstants.ViewModeDetails,
                ref _persistedDetailsColumnWidths);
        }
    }
}
