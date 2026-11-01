using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Mathematics;
using UnityEngine;
using Manager;
using VoxelEngine;
using World.Terrain.Planet;

namespace GravLevel
{
    public enum FaceDir { Forward, Backward, Left, Right }

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> LevelKey, FaceKey;
        internal static ConfigEntry<bool> AlwaysLevel, YawSnap, UseShipFront, Verbose;
        internal static ConfigEntry<FaceDir> FaceDirection;
        internal static ConfigEntry<float> Strength, Damping, MaxAngularAccel, YawSnapDegrees;

        private void Awake()
        {
            Log = Logger;
            LevelKey = Config.Bind("Controls", "LevelKey", new KeyboardShortcut(KeyCode.B),
                "Hold while dragging with the anti-grav tool to level the held object. Default B (vanilla R = Rotate Right, used by the grav tool for distance; V = Scanner; middle mouse = Action Wheel/Free Look).");
            FaceKey = Config.Bind("Controls", "FaceKey", new KeyboardShortcut(KeyCode.N),
                "Hold while dragging with the anti-grav tool to level the held object AND turn its front toward you (replaces yaw snap while held). Default N (unbound in vanilla; next to B. M = Toggle Symmetry, H = Helmet/Holomap, G = Grid Size/Landing Gear).");
            AlwaysLevel = Config.Bind("Controls", "AlwaysLevelWhileHolding", false, "Level continuously while holding anything, without pressing the key.");
            YawSnap = Config.Bind("Leveling", "YawSnap", true, "Also turn the object's heading to the nearest YawSnapDegrees step around the up axis.");
            YawSnapDegrees = Config.Bind("Leveling", "YawSnapDegrees", 90f, "Yaw snap step in degrees.");
            FaceDirection = Config.Bind("Leveling", "FaceDirection", FaceDir.Forward, "Which side of the object is its front for FaceKey: Forward, Backward, Left or Right.");
            UseShipFront = Config.Bind("Leveling", "UseShipFront", true, "For crafts with a yoke/computer, use the pilot's forward (yoke orientation) as the front instead of the build grid's forward.");
            Strength = Config.Bind("Leveling", "Strength", 25f, "Proportional gain (1/s^2). Higher = snappier.");
            Damping = Config.Bind("Leveling", "Damping", 10f, "Angular velocity damping gain (1/s). ~2*sqrt(Strength) is critically damped.");
            MaxAngularAccel = Config.Bind("Leveling", "MaxAngularAccel", 20f, "Clamp on commanded angular acceleration (rad/s^2) before inertia scaling.");
            Verbose = Config.Bind("Debug", "VerboseLogging", false, "Log debug info once per second while leveling.");
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"GravLevel {MyPluginInfo.PLUGIN_VERSION} loaded. Key={LevelKey.Value} FaceKey={FaceKey.Value} FaceDir={FaceDirection.Value} ShipFront={UseShipFront.Value} Always={AlwaysLevel.Value} YawSnap={YawSnap.Value} Kp={Strength.Value} Kd={Damping.Value}");
        }
    }

    [HarmonyPatch(typeof(ActionAntigrav), nameof(ActionAntigrav.LeftClick))]
    internal static class AntigravPatch
    {
        static readonly AccessTools.FieldRef<ActionAntigrav, ParentEntity> Grabbed =
            AccessTools.FieldRefAccess<ActionAntigrav, ParentEntity>("entityGrabbed");
        static float nextLog;

        static void Postfix(ActionAntigrav __instance)
        {
            try
            {
                ParentEntity e = Grabbed(__instance);
                if (e == null) return;
                bool face = Held(Plugin.FaceKey.Value);
                if (!face && !Plugin.AlwaysLevel.Value && !Held(Plugin.LevelKey.Value)) return;

                var body = e.Rigidbody.rigidbody;
                if (!body.IsValid) return;
                var data = body.Data;
                var tf = e.TransformData;

                // Up direction: planet gravity at the entity (same call as the artificial horizon), else camera up.
                Vector3 g = PlanetCommon.GetGravityForce(tf.systemPosition, PlanetResources.PlanetGravityDatas, SkyTransformData.datas);
                bool planet = g.sqrMagnitude > 4e-6f;
                var cam = __instance.commandManager != null ? __instance.commandManager.CameraTransform : null;
                Vector3 up;
                if (planet) up = -g.normalized;
                else up = cam != null ? cam.up : Vector3.up;

                Quaternion rot = tf.rotation;
                Vector3 curUp = rot * Vector3.up;
                Quaternion target = Quaternion.FromToRotation(curUp, up) * rot; // minimal tilt
                bool faced = false;
                string frontSrc = "-";
                Vector3 localFront = Vector3.zero;
                if (face && cam != null)
                {
                    // Same world-space points vanilla uses for the grab: camera ray origin and the entity's center of mass.
                    Vector3 toPlayer = Vector3.ProjectOnPlane(cam.Ray().origin - tf.LocalToWorld(e.Rigidbody.ownCenterOfMass), up);
                    Quaternion front = ShipFront(e, out frontSrc);
                    localFront = front * FaceVector(Plugin.FaceDirection.Value);
                    Vector3 fwd = Vector3.ProjectOnPlane(target * localFront, up);
                    if (toPlayer.sqrMagnitude > 0.01f && fwd.sqrMagnitude > 1e-6f)
                    {
                        target = Quaternion.AngleAxis(Vector3.SignedAngle(fwd, toPlayer, up), up) * target;
                        faced = true;
                    }
                }
                if (!faced && Plugin.YawSnap.Value && Plugin.YawSnapDegrees.Value > 0.1f)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(target * Vector3.forward, up);
                    Vector3 refDir = Vector3.ProjectOnPlane(Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right, up).normalized;
                    if (fwd.sqrMagnitude > 1e-6f)
                    {
                        float yaw = Vector3.SignedAngle(refDir, fwd, up);
                        float step = Plugin.YawSnapDegrees.Value;
                        float snapped = Mathf.Round(yaw / step) * step;
                        target = Quaternion.AngleAxis(snapped - yaw, up) * target;
                    }
                }

                Quaternion err = target * Quaternion.Inverse(rot);
                if (err.w < 0) { err.x = -err.x; err.y = -err.y; err.z = -err.z; err.w = -err.w; }
                err.ToAngleAxis(out float angDeg, out Vector3 axis);
                if (float.IsNaN(axis.x) || float.IsInfinity(axis.x)) axis = Vector3.zero;
                Vector3 angErr = axis * (angDeg * Mathf.Deg2Rad);

                Vector3 w = (Vector3)data.angularVelocity;
                Vector3 alpha = Plugin.Strength.Value * angErr - Plugin.Damping.Value * w;
                alpha = Vector3.ClampMagnitude(alpha, Plugin.MaxAngularAccel.Value);

                // torque = I_world * alpha, I_world = R * I_local * R^T
                float3x3 R = new float3x3(tf.rotationMath);
                float3 torque = math.mul(R, math.mul(data.localInertiaTensor, math.mul(math.transpose(R), (float3)alpha)));
                if (!math.all(math.isfinite(torque))) return;
                body.AddTorque((Vector3)torque);

                if (Plugin.Verbose.Value && Time.time >= nextLog)
                {
                    nextLog = Time.time + 1f;
                    Plugin.Log.LogInfo($"[GravLevel] {e.name} mass={data.mass:F1} src={(planet ? "planet" : "camera")} face={faced} front={frontSrc} localFront={localFront} up={up} err={angDeg:F1}deg w={w.magnitude:F2} torque={math.length(torque):F1}");
                }
            }
            catch (Exception ex)
            {
                if (Time.time >= nextLog) { nextLog = Time.time + 5f; Plugin.Log.LogError(ex); }
            }
        }

        static bool Held(KeyboardShortcut k) =>
            k.MainKey != KeyCode.None && Input.GetKey(k.MainKey) && k.Modifiers.IsNullOrEmptyOr(m => Input.GetKey(m));

        // The game's forward thrust axis is inputOrientation * forward, where inputOrientation is the yoke brick's rotation
        // (ComputerSpaceship.ComputeThrust / SetForwardDirection). Read it from the computer, else from the saved ship orientation.
        static Quaternion ShipFront(ParentEntity e, out string src)
        {
            src = "grid";
            if (!Plugin.UseShipFront.Value) return Quaternion.identity;
            Quaternion q = e.Systems != null ? e.Systems.GetVehicleOrientation() : Quaternion.identity;
            if (ValidNonIdentity(q)) { src = "ship"; return q; }
            q = Quaternion.Inverse(e.EntityMetaData.ShipOrientationInv);
            if (ValidNonIdentity(q)) { src = "ship(saved)"; return q; }
            return Quaternion.identity;
        }

        static bool ValidNonIdentity(Quaternion q)
        {
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return n > 0.5f && Mathf.Abs(q.w) < 0.9999f;
        }

        static Vector3 FaceVector(FaceDir d)
        {
            switch (d)
            {
                case FaceDir.Backward: return Vector3.back;
                case FaceDir.Left: return Vector3.left;
                case FaceDir.Right: return Vector3.right;
                default: return Vector3.forward;
            }
        }

        static bool IsNullOrEmptyOr(this System.Collections.Generic.IEnumerable<KeyCode> mods, Func<KeyCode, bool> pred)
        {
            if (mods == null) return true;
            foreach (var m in mods) if (!pred(m)) return false;
            return true;
        }
    }
}
