using GameFramework.Sound;

namespace GodotGameFramework.Sound
{
    public abstract partial class SoundHelperBase : GodotComponent, ISoundHelper
    {
        public abstract void ReleaseSoundAsset(object soundAsset);
    }
}
