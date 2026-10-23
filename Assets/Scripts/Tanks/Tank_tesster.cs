using UnityEngine;
using IronWasteland.Tanks;

public class TankTester : MonoBehaviour
{
    [SerializeField] private TankDefinition tankDefinition;
    [SerializeField] private int playerLevel = 1;
    [SerializeField] private Vector2 spawnPosition = Vector2.zero;

    private void Start()
    {
        if (tankDefinition == null) return;

        var context = new TankBrainContext
        {
            Level = playerLevel,
            IdView = 0,
            FirepowerEquipmentId = 0,
            DefenseEquipmentId = 0,
            MobilityEquipmentId = 0
        };

        // Gọi hàm khởi tạo chuẩn của hệ thống IronWasteland
        TankBrain spawnedTank = tankDefinition.CreateTank(context);
        if (spawnedTank != null)
        {
            spawnedTank.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, 0f);
            Debug.Log($"[TankTester] Đã spawn thành công xe tăng: {spawnedTank.name}");
        }
    }
}