using System;

namespace BBBNexus
{
    /// <summary>
    /// .ammo VFS 文件节点的数据结构。
    /// 存储武器的实时弹药状态。
    /// </summary>
    [Serializable]
    public class AmmoStateData
    {
        /// <summary>
        /// 当前弹匣内的子弹数
        /// </summary>
        public int CurrentMagazine;

        /// <summary>
        /// 备用弹药总数
        /// </summary>
        public int ReserveAmmo;

        /// <summary>
        /// 射击次数统计
        /// </summary>
        public int ShotsFired;

        public AmmoStateData()
        {
            CurrentMagazine = 0;
            ReserveAmmo = 0;
            ShotsFired = 0;
        }
    }

    /// <summary>
    /// .reload VFS 文件节点的数据结构。
    /// 存储武器的换弹状态。
    /// </summary>
    [Serializable]
    public class ReloadStateData
    {
        /// <summary>
        /// 是否正在换弹
        /// </summary>
        public bool IsReloading;

        /// <summary>
        /// 换弹开始时间
        /// </summary>
        public float ReloadStartTime;

        /// <summary>
        /// 换弹结束时间
        /// </summary>
        public float ReloadEndTime;

        public ReloadStateData()
        {
            IsReloading = false;
            ReloadStartTime = 0f;
            ReloadEndTime = 0f;
        }
    }
}
