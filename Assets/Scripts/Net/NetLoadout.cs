using System;
using System.IO;
using Unity.Netcode;

namespace IronWasteland.Net
{
    /// <summary>
    /// Lựa chọn xe của người chơi, gửi kèm <c>ConnectionData</c> khi vào phòng
    /// để host dựng đúng loại xe, đúng cấp và đúng biến thể kỹ năng.
    /// Đồng thời được replicate xuống mọi máy để client khởi tạo xe bằng đúng chỉ số.
    /// </summary>
    [Serializable]
    public struct NetLoadout : INetworkSerializable
    {
        /// <summary>Chỉ số xe trong roster của <see cref="NetSpawnService"/>.</summary>
        public byte TankIndex;

        /// <summary>Cấp xe (1..90).</summary>
        public int Level;

        /// <summary>ID phụ kiện trong <c>EquipmentDatabase</c>; -1 = không trang bị.</summary>
        public int EquipmentId;

        /// <summary>Biến thể kỹ năng tuỳ chọn 1 (0..2).</summary>
        public int OptionSkill1;

        /// <summary>Biến thể kỹ năng tuỳ chọn 2 (0..2).</summary>
        public int OptionSkill2;

        /// <summary>Đóng gói thành payload nhị phân 17 byte để gửi trong ConnectionData.</summary>
        public byte[] ToPayload()
        {
            using (MemoryStream stream = new MemoryStream(17))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(TankIndex);
                writer.Write(Level);
                writer.Write(EquipmentId);
                writer.Write(OptionSkill1);
                writer.Write(OptionSkill2);
                return stream.ToArray();
            }
        }

        /// <summary>Ghi trực tiếp vào FastBufferWriter để gửi qua CustomMessagingManager.</summary>
        public void WriteTo(FastBufferWriter writer)
        {
            writer.WriteValueSafe(TankIndex);
            writer.WriteValueSafe(Level);
            writer.WriteValueSafe(EquipmentId);
            writer.WriteValueSafe(OptionSkill1);
            writer.WriteValueSafe(OptionSkill2);
        }

        /// <summary>Đọc loadout từ FastBufferReader.</summary>
        public static bool TryFromReader(FastBufferReader reader, out NetLoadout loadout)
        {
            loadout = default;
            try
            {
                reader.ReadValueSafe(out loadout.TankIndex);
                reader.ReadValueSafe(out loadout.Level);
                reader.ReadValueSafe(out loadout.EquipmentId);
                reader.ReadValueSafe(out loadout.OptionSkill1);
                reader.ReadValueSafe(out loadout.OptionSkill2);
                loadout.Level = Math.Clamp(loadout.Level, 1, 90);
                loadout.EquipmentId = Math.Max(-1, loadout.EquipmentId);
                loadout.OptionSkill1 = Math.Clamp(loadout.OptionSkill1, 0, 2);
                loadout.OptionSkill2 = Math.Clamp(loadout.OptionSkill2, 0, 2);
                return true;
            }
            catch
            {
                loadout = default;
                return false;
            }
        }

        /// <summary>Đọc payload do client gửi lên; trả false nếu thiếu dữ liệu hoặc sai định dạng.</summary>
        public static bool TryFromPayload(byte[] payload, out NetLoadout loadout)
        {
            loadout = default;
            if (payload == null || payload.Length < 17) return false;

            try
            {
                using (MemoryStream stream = new MemoryStream(payload, false))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    loadout.TankIndex = reader.ReadByte();
                    loadout.Level = Math.Clamp(reader.ReadInt32(), 1, 90);
                    loadout.EquipmentId = Math.Max(-1, reader.ReadInt32());
                    loadout.OptionSkill1 = Math.Clamp(reader.ReadInt32(), 0, 2);
                    loadout.OptionSkill2 = Math.Clamp(reader.ReadInt32(), 0, 2);
                }
                return true;
            }
            catch (EndOfStreamException)
            {
                loadout = default;
                return false;
            }
        }

        /// <summary>Replicate lựa chọn xe xuống mọi máy qua NetworkVariable.</summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TankIndex);
            serializer.SerializeValue(ref Level);
            serializer.SerializeValue(ref EquipmentId);
            serializer.SerializeValue(ref OptionSkill1);
            serializer.SerializeValue(ref OptionSkill2);
        }
    }
}
