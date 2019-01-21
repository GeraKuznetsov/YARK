package Client;

import Client.Util.BufferReader;

public class VesselPacket {

    public static final int LENGTH = 223;

    private int ID;

    public float deltaTime;

    // ##### CRAFT ######
    public float Pitch;
    public float Heading;
    public float Roll;

    // #### NAVBALL VECTOR #######
    public NavHeading Prograde;
    public NavHeading Target;
    public NavHeading Maneuver;

    public short MainControls; // SAS RCS Lights Gear Brakes Abort Stage
    public short ActionGroups; // action groups 1-10 in 2 bytes
    public float VVI;
    public float G;
    public float RAlt;
    public float Alt;
    public float Vsurf;
    public byte MaxOverHeat; // Max part overheat (% percent)
    public float IAS; // Indicated Air Speed

    // ###### ORBITAL ######
    public float VOrbit;
    public float AP;
    public float PE;
    public int TAp;
    public int TPe;
    public float SemiMajorAxis;
    public float SemiMinorAxis;
    public float e;
    public float inc;
    public int period;
    public float TrueAnomaly;
    public float Lat;
    public float Lon;

    // ###### FUEL #######
    public byte CurrentStage; // Current stage number
    public byte TotalStage; // TotalNumber of stages
    public float LiquidFuelTot;
    public float LiquidFuel;
    public float OxidizerTot;
    public float Oxidizer;
    public float EChargeTot;
    public float ECharge;
    public float MonoPropTot;
    public float MonoProp;
    public float IntakeAirTot;
    public float IntakeAir;
    public float SolidFuelTot;
    public float SolidFuel;
    public float XenonGasTot;
    public float XenonGas;
    public float LiquidFuelTotS;
    public float LiquidFuelS;
    public float OxidizerTotS;
    public float OxidizerS;

    // ### MISC ###
    public int MissionTime;
    public int MNTime;
    public float MNDeltaV;
    public boolean HasTarget;
    public float TargetDist; // Distance to targeted vessel (m)
    public float TargetV; // Target vessel relative velocity (m/s)
    public int SOINumber; // SOI Number

    public int SASMode; // hold, prograde, retro, etc...
    public int SpeedMode; // Surface, orbit target

    public int timeWarpRateIndex;

    BufferReader b = new BufferReader();

    public VesselPacket() {
        Prograde = new NavHeading();
        Target = new NavHeading();
        Maneuver = new NavHeading();
    }

    public VesselPacket(byte[] source) {
        Prograde = new NavHeading();
        Target = new NavHeading();
        Maneuver = new NavHeading();

        b.Start(source);
        ID = b.Int();
        deltaTime = b.Float();
        Pitch = b.Float();
        Heading = b.Float();
        Roll = b.Float();
        Prograde.Pitch = b.Float();
        Prograde.Heading = b.Float();
        Target.Pitch = b.Float();
        Target.Heading = b.Float();
        Maneuver.Pitch = b.Float();
        Maneuver.Heading = b.Float();
        MainControls = b.Byte();
        ActionGroups = b.Short();
        VVI = b.Float();
        G = b.Float();
        RAlt = b.Float();
        Alt = b.Float();
        Vsurf = b.Float();
        MaxOverHeat = b.Byte();
        IAS = b.Float();
        VOrbit = b.Float();
        AP = b.Float();
        PE = b.Float();
        TAp = b.Int();
        TPe = b.Int();
        SemiMajorAxis = b.Float();
        SemiMinorAxis = b.Float();
        e = b.Float();
        inc = b.Float();
        period = b.Int();
        TrueAnomaly = b.Float();
        Lat = b.Float();
        Lon = b.Float();
        CurrentStage = b.Byte();
        TotalStage = b.Byte();
        LiquidFuelTot = b.Float();
        LiquidFuel = b.Float();
        OxidizerTot = b.Float();
        Oxidizer = b.Float();
        EChargeTot = b.Float();
        ECharge = b.Float();
        MonoPropTot = b.Float();
        MonoProp = b.Float();
        IntakeAirTot = b.Float();
        IntakeAir = b.Float();
        SolidFuelTot = b.Float();
        SolidFuel = b.Float();
        XenonGasTot = b.Float();
        XenonGas = b.Float();
        LiquidFuelTotS = b.Float();
        LiquidFuelS = b.Float();
        OxidizerTotS = b.Float();
        OxidizerS = b.Float();
        MissionTime = b.Int();
        MNTime = b.Int();
        MNDeltaV = b.Float();
        HasTarget = b.Bool();
        TargetDist = b.Float();
        TargetV = b.Float();
        SOINumber = b.IntFromByte();
        SASMode = b.IntFromByte();
        SpeedMode = b.IntFromByte();
        timeWarpRateIndex = b.IntFromByte();
    }

    public boolean GetActionGroup(int group) {
        return (ActionGroups & group) != 0;
    }

    public boolean GetMainControl(int control) {
        return (MainControls & control) != 0;
    }

    public int getID() {
        return ID;
    }
}
