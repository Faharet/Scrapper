using UnityEngine;

public class RollingBarrelEnemy : MonoBehaviour
{
    private Rigidbody2D rb;
    private Transform player;
    public float rollSpeed = 5f;
    public float rotationSpeed = 200f;
    public float activationDistance = 4f;
    private bool isActive = false;
    
    void Start()
    {
        // Получаем компонент Rigidbody2D
        rb = GetComponent<Rigidbody2D>();
        
        // Проверяем наличие Rigidbody2D
        if (rb == null)
        {
            Debug.LogError("Rigidbody2D component missing on RollingBarrelEnemy!");
            return;
        }
        
        // Находим игрока
        FindPlayer();
        
        // Настраиваем физику
        rb.gravityScale = 2f;
        rb.freezeRotation = false;
        
        // Изначально останавливаем бочку
        StopRolling();
        
        Debug.Log("Barrel initialized. Waiting for player...");
    }
    
    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log("Player found: " + player.name);
        }
        else
        {
            Debug.LogError("Player not found! Make sure player has 'Player' tag.");
        }
    }
    
    void Update()
    {
        // Если игрок не найден, пытаемся найти снова
        if (player == null)
        {
            FindPlayer();
            return;
        }
        
        // Проверяем расстояние до игрока
        if (!isActive)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            
            Debug.Log($"Distance to player: {distanceToPlayer}, Activation: {activationDistance}, Active: {distanceToPlayer <= activationDistance}");
            
            if (distanceToPlayer <= activationDistance)
            {
                // Игрок в зоне активации - запускаем движение
                isActive = true;
                StartRolling();
                Debug.Log("Barrel ACTIVATED! Player distance: " + distanceToPlayer);
            }
        }
    }
    
    void FixedUpdate()
    {
        // Проверяем наличие Rigidbody2D и активность
        if (rb == null || !isActive) return;
        
        // Постоянное движение
        rb.linearVelocity = new Vector2(-rollSpeed, rb.linearVelocity.y);
        
        // Вращение бочки
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
    
    void StartRolling()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(-rollSpeed, 0f);
            Debug.Log("Barrel started rolling!");
        }
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isActive) return;
        
        // Разворот при столкновении со стенами
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Ground"))
        {
            rollSpeed = -rollSpeed;
            rotationSpeed = -rotationSpeed;
            Debug.Log("Barrel changed direction!");
        }
    }
    
    // Визуализация зоны активации в редакторе
    void OnDrawGizmosSelected()
    {
        // Рисуем зону активации
        Gizmos.color = isActive ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);
    }
    
    void StopRolling()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }
}