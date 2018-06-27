﻿using System;
using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields 
    {
        #region Setup
        private uint _tick;
        private uint _enforceTick;
        private uint _hierarchyTick = 1;
        public float ImpactSize { get; set; } = 9f;
        public float Absorb { get; set; }
        private float _power = 0.0001f;
        private float _gridMaxPower;
        private float _gridCurrentPower;
        private float _gridAvailablePower;
        private float _shieldMaxBuffer;
        private float _shieldMaxChargeRate;
        private float _shieldChargeRate;
        private float _shieldDps;
        private float _shieldCurrentPower;
        private float _shieldMaintaintPower;
        private float _shieldConsumptionRate;
        private float _shieldFudge;

        private double _ellipsoidAdjust = Math.Sqrt(2);
        private double _oldEllipsoidAdjust;
        private double _sAvelSqr;
        private double _ellipsoidSurfaceArea;
        private double _sizeScaler;

        public int BulletCoolDown { get; private set; } = -1;
        public int EntityCoolDown { get; private set; } = -1;
        private int _count = -1;
        private int _lCount;
        private int _eCount;
        private int _randomCount = -1;
        private int _shieldDownLoop = -1;
        private int _genericDownLoop = -1;
        private int _reModulationLoop = -1;
        private const int ReModulationCount = 300;
        private const int ShieldDownCount = 1200;
        private const int GenericDownCount = 60;

        private int _prevLod;
        private int _onCount;
        private int _oldBlockCount;
        private int _shieldRatio;

        internal bool DeformEnabled;
        internal bool ControlBlockWorking;
        internal bool MainInit;
        internal bool PhysicsInit;
        internal bool AllInited;
        internal bool ShieldOffline;
        internal bool CheckGridRegister;
        internal bool WarmedUp;
        internal bool UpdateDimensions;
        internal bool HardDisable { get; private set; }
        internal bool GridIsMobile;
        private bool _blocksChanged;
        private bool _prevShieldActive;
        private bool _effectsCleanup;
        private bool _hideShield;
        private bool _shapeAdjusted;
        private bool _fitChanged;
        private bool _hierarchyDelayed;
        private bool _entityChanged = true;
        private bool _enablePhysics = true;
        private bool _shapeLoaded = true;

        private const string SpaceWolf = "Space_Wolf";
        private const string MyMissile = "MyMissile";
        private const string MyDebrisBase = "MyDebrisBase";

        private Vector2D _shieldIconPos = new Vector2D(-0.91, -0.87);

        internal Vector3D DetectionCenter;
        internal Vector3D WorldImpactPosition { get; set; } = new Vector3D(Vector3D.NegativeInfinity);
        internal Vector3D ShieldSize { get; set; }
        private Vector3D _localImpactPosition;
        private Vector3D _gridHalfExtents;
        private Vector3D _oldGridHalfExtents;

        internal MatrixD DetectMatrixOutsideInv;
        private MatrixD _shieldGridMatrix;
        private MatrixD _shieldShapeMatrix;
        private MatrixD _detectMatrixOutside;
        private MatrixD _detectMatrixInside;
        private MatrixD _detectInsideInv;
        private MatrixD _expandedMatrix;
        private MatrixD _viewProjInv;

        private BoundingBox _shieldAabb;
        private BoundingBox _expandedAabb;
        private BoundingSphereD _shieldSphere;
        private MyOrientedBoundingBoxD _sOriBBoxD;
        private Quaternion _sQuaternion;
        private readonly Random _random = new Random();
        private readonly List<MyResourceSourceComponent> _powerSources = new List<MyResourceSourceComponent>();
        private readonly List<MyCubeBlock> _functionalBlocks = new List<MyCubeBlock>();
        private readonly List<KeyValuePair<IMyEntity, EntIntersectInfo>> _webEntsTmp = new List<KeyValuePair<IMyEntity, EntIntersectInfo>>();

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private readonly DataStructures _dataStructures = new DataStructures();
        //private readonly StructureBuilder _structureBuilder = new StructureBuilder();

        internal readonly HashSet<IMyEntity> FriendlyCache = new HashSet<IMyEntity>();
        internal readonly HashSet<IMyEntity> PartlyProtectedCache = new HashSet<IMyEntity>();
        internal readonly HashSet<IMyEntity> IgnoreCache = new HashSet<IMyEntity>();
        private List<IMyCubeGrid> _connectedGrids = new List<IMyCubeGrid>();

        private MyConcurrentDictionary<IMyEntity, Vector3D> Eject { get; } = new MyConcurrentDictionary<IMyEntity, Vector3D>();
        private readonly MyConcurrentDictionary<IMyEntity, EntIntersectInfo> _webEnts = new MyConcurrentDictionary<IMyEntity, EntIntersectInfo>();

        private readonly Dictionary<long, DefenseShields> _shields = new Dictionary<long, DefenseShields>();

        private readonly MyConcurrentQueue<IMySlimBlock> _dmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMySlimBlock> _fewDmgBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyEntity> _missileDmg = new MyConcurrentQueue<IMyEntity>();
        private readonly MyConcurrentQueue<IMyMeteor> _meteorDmg = new MyConcurrentQueue<IMyMeteor>();
        private readonly MyConcurrentQueue<IMySlimBlock> _destroyedBlocks = new MyConcurrentQueue<IMySlimBlock>();
        private readonly MyConcurrentQueue<IMyCubeGrid> _staleGrids = new MyConcurrentQueue<IMyCubeGrid>();
        private readonly MyConcurrentQueue<IMyCharacter> _characterDmg = new MyConcurrentQueue<IMyCharacter>();
        private readonly MyConcurrentQueue<MyVoxelBase> _voxelDmg = new MyConcurrentQueue<MyVoxelBase>();

        private static readonly MyStringId _hudIconOffline = MyStringId.GetOrCompute("DS_ShieldOffline");
        private static readonly MyStringId _hudIconHealth10 = MyStringId.GetOrCompute("DS_ShieldHealth10");
        private static readonly MyStringId _hudIconHealth20 = MyStringId.GetOrCompute("DS_ShieldHealth20");
        private static readonly MyStringId _hudIconHealth30 = MyStringId.GetOrCompute("DS_ShieldHealth30");
        private static readonly MyStringId _hudIconHealth40 = MyStringId.GetOrCompute("DS_ShieldHealth40");
        private static readonly MyStringId _hudIconHealth50 = MyStringId.GetOrCompute("DS_ShieldHealth50");
        private static readonly MyStringId _hudIconHealth60 = MyStringId.GetOrCompute("DS_ShieldHealth60");
        private static readonly MyStringId _hudIconHealth70 = MyStringId.GetOrCompute("DS_ShieldHealth70");
        private static readonly MyStringId _hudIconHealth80 = MyStringId.GetOrCompute("DS_ShieldHealth80");
        private static readonly MyStringId _hudIconHealth90 = MyStringId.GetOrCompute("DS_ShieldHealth90");
        private static readonly MyStringId _hudIconHealth100 = MyStringId.GetOrCompute("DS_ShieldHealth100");

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        internal IMyOreDetector Shield => (IMyOreDetector)Entity;
        internal ShieldType ShieldMode;
        internal MyEntity ShieldEnt;
        private MyEntity _shellPassive;
        private MyEntity _shellActive;

        internal Icosphere.Instance Icosphere;
        internal readonly Spawn Spawn = new Spawn();
        internal readonly EllipsoidOxygenProvider EllipsoidOxyProvider = new EllipsoidOxygenProvider(Matrix.Zero);
        internal readonly EllipsoidSA EllipsoidSa = new EllipsoidSA(double.MinValue, double.MinValue, double.MinValue);

        internal DSUtils Dsutil1 = new DSUtils();
        internal DSUtils Dsutil2 = new DSUtils();
        internal DSUtils Dsutil3 = new DSUtils();
        internal DSUtils Dsutil4 = new DSUtils();
        internal DSUtils Dsutil5 = new DSUtils();

        public MyModStorageComponentBase Storage { get; set; }
        internal DefenseShieldsSettings DsSet;
        internal ShieldGridComponent ShieldComp;

        internal HashSet<ulong> playersToReceive = null;

        internal MyStringId CustomDataTooltip = MyStringId.GetOrCompute("Shows an Editor for custom data to be used by scripts and mods");
        internal MyStringId CustomData = MyStringId.GetOrCompute("CustomData");
        internal MyStringId Password = MyStringId.GetOrCompute("Password");
        internal MyStringId PasswordTooltip = MyStringId.GetOrCompute("Set the shield modulation password");

        public bool Enabled
        {
            get { return DsSet.Settings.Enabled; }
            set { DsSet.Settings.Enabled = value; }
        }

        public bool ShieldPassiveHide
        {
            get { return DsSet.Settings.PassiveInvisible; }
            set { DsSet.Settings.PassiveInvisible = value; }
        }

        public bool ShieldActiveHide
        {
            get { return DsSet.Settings.ActiveInvisible; }
            set { DsSet.Settings.ActiveInvisible = value; }
        }

        public float Width
        {
            get { return DsSet.Settings.Width; }
            set { DsSet.Settings.Width = value; }
        }

        public float Height
        {
            get { return DsSet.Settings.Height; }
            set { DsSet.Settings.Height = value; }
        }

        public float Depth
        {
            get { return DsSet.Settings.Depth; }
            set { DsSet.Settings.Depth = value; }
        }

        public float Rate
        {
            get { return DsSet.Settings.Rate; }
            set { DsSet.Settings.Rate = value; }
        }

        public bool ExtendFit
        {
            get { return DsSet.Settings.ExtendFit; }
            set { DsSet.Settings.ExtendFit = value; }
        }

        public bool SphereFit
        {
            get { return DsSet.Settings.SphereFit; }
            set { DsSet.Settings.SphereFit = value; }
        }

        public bool FortifyShield
        {
            get { return DsSet.Settings.FortifyShield; }
            set { DsSet.Settings.FortifyShield = value; }
        }

        public bool SendToHud
        {
            get { return DsSet.Settings.SendToHud; }
            set { DsSet.Settings.SendToHud = value; }
        }

        public float ShieldBuffer
        {
            get { return DsSet.Settings.Buffer; }
            set { DsSet.Settings.Buffer = value; }
        }

        public bool ModulateVoxels
        {
            get { return DsSet.Settings.ModulateVoxels; }
            set { DsSet.Settings.ModulateVoxels = value; }
        }

        public bool ModulateGrids
        {
            get { return DsSet.Settings.ModulateGrids; }
            set { DsSet.Settings.ModulateGrids = value; }
        }
        #endregion

        #region constructors
        private MatrixD DetectionMatrix
        {
            get { return _detectMatrixOutside; }
            set
            {
                _detectMatrixOutside = value;
                DetectMatrixOutsideInv = MatrixD.Invert(value);
                _detectMatrixInside = MatrixD.Rescale(value, 1d + (-6.0d / 100d));
                _detectInsideInv = MatrixD.Invert(_detectMatrixInside);
            }
        }
        #endregion
    }
}
