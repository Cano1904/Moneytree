using System.Collections.Generic;
using Glasscore.Simulation;

namespace Glasscore.Net
{
    public static class Protocol
    {
        public const ushort Version = 4;
        public const ushort UdpMagic = 0x4C47;      // "GL"
        public const int DefaultPort = 27015;
        public const int DiscoveryPort = 27100;
        public const int MaxReliableFrame = 64 * 1024;
        public const int InputRedundancy = 8;       // each input packet repeats the last N inputs
        public const float InterpolationDelay = 0.1f;
        public const float ReconnectGrace = 60f;
        public const byte SystemSlot = 255;
    }

    public enum MsgType : byte
    {
        // ── reliable (TCP) client → server
        Hello = 1,
        SetProfile,
        SetReady,
        HostSettings,
        HostKick,
        HostStart,
        Chat,
        SceneLoaded,
        LeaveMatch,
        HostAddBot,

        // ── reliable (TCP) server → client
        Welcome = 32,
        Reject,
        LobbyState,
        ChatBroadcast,
        MatchStart,
        TileState,
        TileDelta,
        ShatterTile,        // RPC_ShatterTile(int tileID, Vector3 impactPoint, float force)
        MatchPhase,
        WorldEvent,
        Highlights,
        Results,
        ReturnToLobby,
        Kicked,
        LobbyCountdown,

        // ── unreliable (UDP)
        Input = 64,
        Snapshot,
        Ping,
        Pong,
        Voice,
        UdpHello,
    }

    public sealed class LobbySettings
    {
        public byte MapId;
        public byte MaxPlayers = 8;
        public bool FriendlyFire;
        public byte MaxScore = MatchTimings.DefaultMaxScore;
        public bool IsPublic = true;
        public bool Teams;
        public bool Competitive;

        public void Write(NetWriter w)
        {
            w.Byte(MapId); w.Byte(MaxPlayers); w.Bool(FriendlyFire); w.Byte(MaxScore); w.Bool(IsPublic); w.Bool(Teams); w.Bool(Competitive);
        }

        public static LobbySettings Read(ref NetReader r) => new LobbySettings
        {
            MapId = r.Byte(), MaxPlayers = r.Byte(), FriendlyFire = r.Bool(), MaxScore = r.Byte(), IsPublic = r.Bool(), Teams = r.Bool(), Competitive = r.Bool(),
        };

        public LobbySettings Clone() => (LobbySettings)MemberwiseClone();
    }

    public sealed class RosterEntry
    {
        public byte Slot;
        public string Name;
        public byte Skin;
        public byte Trail;
        public bool Ready;
        public ushort PingMs;
        public byte Team;
        public int Mmr;
        public bool Connected = true;

        public void Write(NetWriter w)
        {
            w.Byte(Slot); w.String(Name, 64); w.Byte(Skin); w.Byte(Trail); w.Bool(Ready); w.UShort(PingMs); w.Byte(Team); w.Int(Mmr); w.Bool(Connected);
        }

        public static RosterEntry Read(ref NetReader r) => new RosterEntry
        {
            Slot = r.Byte(), Name = r.String(), Skin = r.Byte(), Trail = r.Byte(), Ready = r.Bool(), PingMs = r.UShort(), Team = r.Byte(), Mmr = r.Int(), Connected = r.Bool(),
        };
    }

    public sealed class LobbyView
    {
        public string Code = string.Empty;
        public string ServerName = string.Empty;
        public byte HostSlot;
        public bool InMatch;
        public LobbySettings Settings = new LobbySettings();
        public List<RosterEntry> Roster = new List<RosterEntry>();

        public RosterEntry Find(int slot)
        {
            foreach (var e in Roster) if (e.Slot == slot) return e;
            return null;
        }

        public void Write(NetWriter w)
        {
            w.String(Code, 16); w.String(ServerName, 64); w.Byte(HostSlot); w.Bool(InMatch);
            Settings.Write(w);
            w.Byte((byte)Roster.Count);
            foreach (var e in Roster) e.Write(w);
        }

        public static LobbyView Read(ref NetReader r)
        {
            var v = new LobbyView { Code = r.String(), ServerName = r.String(), HostSlot = r.Byte(), InMatch = r.Bool() };
            v.Settings = LobbySettings.Read(ref r);
            int n = r.Byte();
            for (int i = 0; i < n; i++) v.Roster.Add(RosterEntry.Read(ref r));
            return v;
        }
    }

    public struct ProjectileView
    {
        public int Id;
        public byte Owner;
        public byte Weapon;
        public Vec3 Position;
        public Vec3 Velocity;
    }

    public sealed class SnapshotData
    {
        public int ServerTick;
        public uint AckSequence;
        public MatchPhase Phase;
        public int PhaseStartTick;
        public readonly PlayerState[] Players = new PlayerState[GameWorld.MaxPlayers];
        public readonly short[] Scores = new short[GameWorld.MaxPlayers];
        public readonly List<ProjectileView> Projectiles = new List<ProjectileView>();
    }

    [System.Flags]
    public enum PlayerFlags : byte
    {
        Active = 1, Alive = 2, Grounded = 4,
    }

    public static class Codec
    {
        public static void WritePlayer(NetWriter w, in PlayerState p)
        {
            var flags = (PlayerFlags)0;
            if (p.Active) flags |= PlayerFlags.Active;
            if (p.Alive) flags |= PlayerFlags.Alive;
            if (p.Grounded) flags |= PlayerFlags.Grounded;
            w.Byte(p.Slot);
            w.Byte((byte)flags);
            w.Vec3(p.Position);
            w.Vec3(p.Velocity);
            w.Float(p.Yaw);
            w.Float(p.Pitch);
            w.Float(p.Health);
            w.Short((short)p.GroundTile);
            w.Byte(p.Weapon);
            w.Float(p.FireCooldown);
            w.Float(p.AnchorTime);
            w.Float(p.AnchorCooldown);
            w.Float(p.SpawnProtection);
            w.UShort((ushort)p.PrevButtons);
            w.Byte(p.Team);
        }

        public static PlayerState ReadPlayer(ref NetReader r)
        {
            var p = new PlayerState { Slot = r.Byte() };
            var flags = (PlayerFlags)r.Byte();
            p.Active = (flags & PlayerFlags.Active) != 0;
            p.Alive = (flags & PlayerFlags.Alive) != 0;
            p.Grounded = (flags & PlayerFlags.Grounded) != 0;
            p.Position = r.Vec3();
            p.Velocity = r.Vec3();
            p.Yaw = r.Float();
            p.Pitch = r.Float();
            p.Health = r.Float();
            p.GroundTile = r.Short();
            p.Weapon = r.Byte();
            p.FireCooldown = r.Float();
            p.AnchorTime = r.Float();
            p.AnchorCooldown = r.Float();
            p.SpawnProtection = r.Float();
            p.PrevButtons = (InputButtons)r.UShort();
            p.Team = r.Byte();
            return p;
        }

        public static void WriteInput(NetWriter w, in PlayerInput i)
        {
            w.UInt(i.Sequence);
            w.SByte((sbyte)System.Math.Round(GcMath.Clamp(i.MoveX, -1f, 1f) * 127f));
            w.SByte((sbyte)System.Math.Round(GcMath.Clamp(i.MoveY, -1f, 1f) * 127f));
            w.Float(i.Yaw);
            w.Float(i.Pitch);
            w.UShort((ushort)i.Buttons);
        }

        public static PlayerInput ReadInput(ref NetReader r)
        {
            // Quantized identically on both ends: the client predicts with the dequantized values too.
            return new PlayerInput
            {
                Sequence = r.UInt(),
                MoveX = r.SByte() / 127f,
                MoveY = r.SByte() / 127f,
                Yaw = r.Float(),
                Pitch = r.Float(),
                Buttons = (InputButtons)r.UShort(),
            };
        }

        /// <summary>Applies the wire quantization locally so prediction matches the server bit-for-bit.</summary>
        public static PlayerInput QuantizeInput(PlayerInput i)
        {
            i.MoveX = (sbyte)System.Math.Round(GcMath.Clamp(i.MoveX, -1f, 1f) * 127f) / 127f;
            i.MoveY = (sbyte)System.Math.Round(GcMath.Clamp(i.MoveY, -1f, 1f) * 127f) / 127f;
            return i;
        }

        public static void WriteSnapshot(NetWriter w, SnapshotData s, int playerMask)
        {
            w.Int(s.ServerTick);
            w.UInt(s.AckSequence);
            w.Byte((byte)s.Phase);
            w.Int(s.PhaseStartTick);
            w.Byte((byte)playerMask);
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                if ((playerMask & (1 << i)) == 0) continue;
                WritePlayer(w, s.Players[i]);
                w.Short(s.Scores[i]);
            }
            int n = System.Math.Min(s.Projectiles.Count, 255);
            w.Byte((byte)n);
            for (int i = 0; i < n; i++)
            {
                var p = s.Projectiles[i];
                w.Int(p.Id); w.Byte(p.Owner); w.Byte(p.Weapon); w.Vec3(p.Position); w.Vec3(p.Velocity);
            }
        }

        public static void ReadSnapshot(ref NetReader r, SnapshotData s)
        {
            s.ServerTick = r.Int();
            s.AckSequence = r.UInt();
            s.Phase = (MatchPhase)r.Byte();
            s.PhaseStartTick = r.Int();
            int mask = r.Byte();
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    s.Players[i] = new PlayerState { Slot = (byte)i };
                    s.Scores[i] = 0;
                    continue;
                }
                s.Players[i] = ReadPlayer(ref r);
                s.Scores[i] = r.Short();
            }
            s.Projectiles.Clear();
            int n = r.Byte();
            for (int i = 0; i < n; i++)
            {
                s.Projectiles.Add(new ProjectileView { Id = r.Int(), Owner = r.Byte(), Weapon = r.Byte(), Position = r.Vec3(), Velocity = r.Vec3() });
            }
        }
    }

    public struct ResultRow
    {
        public byte Slot;
        public string Name;
        public short Score;
        public short Knockouts;
        public short Deaths;
        public short TilesShattered;
        public byte Placement;
        public short RatingDelta;
        public int NewRating;
    }
}
