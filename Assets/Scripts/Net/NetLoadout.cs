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

        /// <summary>Đọc payload do client gửi lên; trả false nếu thiếu dữ liệu hoặc sai định dạng.</summary>
        public static bool TryFromPayload(byte[] payload, out NetLoadout loadout)
        {            loadout = default;
            if (payload == null || payload.Length < 17) return false;

            try
            {
                using (MemoryStream stream = new MemoryStream(payload, false))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    loadout.TankIndex = reader.ReadByte();
                    loadout.Level = reader.ReadInt32();
                    loadout.EquipmentId = reader.ReadInt32();
                    loadout.OptionSkill1 = reader.ReadInt32();
                    loadout.OptionSkill2 = reader.ReadInt32();
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
