using Unity.Netcode;

namespace IronWasteland.Net
{
    /// <summary>
    /// Thời gian hồi chiêu còn lại của 4 slot kỹ năng, gửi từ host xuống client.
    /// Chỉ ghi khi kỹ năng vừa được dùng hoặc vừa được giảm hồi chiêu; client tự đếm ngược.
    /// </summary>
    public struct CooldownState : INetworkSerializable
    {
        public float Normal;
        public float Specialized;
        public float Optional1;
        public float Optional2;

        public float Get(int index)
        {
            switch (index)
            {
                case 0: return Normal;
                case 1: return Specialized;
                case 2: return Optional1;
                case 3: return Optional2;
                default: return 0f;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Normal);
            serializer.SerializeValue(ref Specialized);
            serializer.SerializeValue(ref Optional1);
            serializer.SerializeValue(ref Optional2);
        }
    }
}
