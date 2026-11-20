// The player's fit saved on the device (PlayerPrefs). Not used in batch mode, so tests never read or write it.
using Labyrinth.Core;
using UnityEngine;

namespace Labyrinth.Game
{
    public sealed class PrefsFitStore : IFitStore
    {
        const string Height = "labyrinth.fit.tableHeight";
        const string Spacing = "labyrinth.fit.leverSpacing";

        public bool TryLoad(out float tableHeight, out float leverSpacing)
        {
            tableHeight = PlayerPrefs.GetFloat(Height, PlayerFit.DefaultTableHeight);
            leverSpacing = PlayerPrefs.GetFloat(Spacing, PlayerFit.DefaultLeverSpacing);
            return PlayerPrefs.HasKey(Height) && PlayerPrefs.HasKey(Spacing);
        }

        public void Save(float tableHeight, float leverSpacing)
        {
            PlayerPrefs.SetFloat(Height, tableHeight);
            PlayerPrefs.SetFloat(Spacing, leverSpacing);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(Height);
            PlayerPrefs.DeleteKey(Spacing);
            PlayerPrefs.Save();
        }
    }
}
