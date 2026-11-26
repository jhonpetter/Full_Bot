using System;
using System.Collections.Generic;
using System.IO;

namespace Full_Bot
{
    public class PlayerMovementFunctions
    {
        private const string ScriptsFolder = "Scripts";
        private const string MoveToScript = "MoveTo.asm";
        private const string SetFlyAltScript = "SetFlyAlt.asm";
        private const string ToggleFlyScript = "ToggleFly.asm";

        private const uint MoveModeGround = 0u;
        private const uint MoveModeSwimming = 1u;
        private const uint MoveModeFlying = 2u;

        private const int MinFlyAltitude = 0;
        private const int MaxFlyAltitude = 69;

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public PlayerMovementFunctions(MemoryProcess mem, AsmRunner asm)
        {
            _mem = mem ?? throw new ArgumentNullException(nameof(mem));
            _asm = asm ?? throw new ArgumentNullException(nameof(asm));
        }

        private static string ResolveScriptPath(string fileName)
        {
            string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ScriptsFolder, fileName);
            if (File.Exists(baseDirPath))
                return baseDirPath;

            return Path.Combine(ScriptsFolder, fileName);
        }

        private static uint FloatToInt32Bits(float value)
        {
            int rounded = Convert.ToInt32(Math.Round(value));
            return unchecked((uint)rounded);
        }

        private static uint ClampAltitudeToUint(int altitude)
        {
            int clamped = altitude;
            if (clamped < MinFlyAltitude) clamped = MinFlyAltitude;
            if (clamped > MaxFlyAltitude) clamped = MaxFlyAltitude;
            return unchecked((uint)clamped);
        }

        public uint GetMoveMode(PlayerCore player = null)
        {
            if (player != null)
                return player.MoveMode;

            return _mem.ReadUIntFromNodeAtBase(0u, "player_move_mode");
        }

        public void ToggleFly()
        {
            _asm.RunScript(_mem, ResolveScriptPath(ToggleFlyScript));
        }

        public void SetFlyAltitude(int altitude)
        {
            uint altHexVal = ClampAltitudeToUint(altitude);
            var vars = new Dictionary<string, uint>
            {
                { "FLY_ALT", altHexVal }
            };
            _asm.RunScript(_mem, ResolveScriptPath(SetFlyAltScript), vars);
        }

        public void MoveToGround(float x, float z)
        {
            var vars = new Dictionary<string, uint>
            {
                { "INT_X", FloatToInt32Bits(x) },
                { "INT_Z", FloatToInt32Bits(z) },
                { "FLY_MODE", MoveModeGround }
            };
            _asm.RunScript(_mem, ResolveScriptPath(MoveToScript), vars);
        }

        public void NavigateTo(float x, float z, int altitude, PlayerCore player = null)
        {
            uint altHexVal = ClampAltitudeToUint(altitude);
            uint currentMode = GetMoveMode(player);

            /*if (currentMode != MoveModeFlying)
            {
                ToggleFly();
            }*/

            var moveVars = new Dictionary<string, uint>
            {
                { "INT_X", FloatToInt32Bits(x) },
                { "INT_Z", FloatToInt32Bits(z) },
                { "FLY_MODE", altHexVal }
            };
            _asm.RunScript(_mem, ResolveScriptPath(MoveToScript), moveVars);

            SetFlyAltitude(altitude);
        }
    }

    public partial class PlayerCore
    {
        private PlayerMovementFunctions _movement;

        public PlayerMovementFunctions Movement
        {
            get
            {
                if (_movement == null)
                    _movement = new PlayerMovementFunctions(_mem, _asm);
                return _movement;
            }
        }

        public void MoveSimple(float x, float z)
        {
            Movement.MoveToGround(x, z);
        }

        public void MoveNavigate(float x, float z, int altitude)
        {
            Movement.NavigateTo(x, z, altitude, this);
        }

        public void ToggleFlyState()
        {
            Movement.ToggleFly();
        }
    }
}