namespace IronWasteland.Net.Models
{
    /// <summary>Một dòng đã định dạng sẵn cho danh sách phòng LAN.</summary>
    public readonly struct LanRoomRow
    {
        public LanRoomRow(string name, string details)
        {
            Name = name;
            Details = details;
        }

        public string Name { get; }
        public string Details { get; }
    }
}
