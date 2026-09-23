namespace MimicCell.Core
{
    public static class MimicSceneNames
    {
        public const string StartMenu = "StartMenu";
        public const string CambrianOcean = "CambrianOcean";
        public const string Modern = "Modern";
        public const string SampleScene = "SampleScene";

        public static bool IsGameplayScene(string sceneName)
        {
            return sceneName == CambrianOcean ||
                   sceneName == Modern ||
                   sceneName == SampleScene;
        }
    }
}
