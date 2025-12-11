using System.Collections.Generic;
using UnityEngine;

public class AgentController : MonoBehaviour
{
    [Header("Riferimenti Scena")]
    [Tooltip("Trascina qui l'oggetto EndPoint")]
    public Transform targetLandmark;
    [Tooltip("Trascina qui l'oggetto Water")]
    public Transform waterObject;
    [Tooltip("Trascina qui l'oggetto Terrain")]
    public Collider terrainCollider;

    [Header("Configurazione Mappa")]
    [Tooltip("Offset iniziale se il terreno non è a 0,0,0")]
    public Vector3 mapOrigin = Vector3.zero;
    [Tooltip("Dimensioni dell'area di gioco")]
    public Vector2Int mapSize = new Vector2Int(50, 100);

    [Header("Parametri Movimento")]
    public float flatSpeed = 1.0f;   // Velocità in piano/discesa
    public float uphillSpeed = 0.5f; // Velocità in salita
    public float stopTolerance = 0.5f; // Distanza per considerare l'arrivo
    
    [Header("Debug")]
    [Tooltip("Attiva per vedere linee e raggi nella Scene View")]
    public bool showDebugGizmos = false;

    // Stato interno
    private Vector3 currentTargetPosition;
    private bool hasTarget = false;
    private bool hasArrived = false;
    private const float STEP_SIZE = 1.0f; // Dimensione passo griglia

    void Start()
    {
        if (!ValidateReferences()) return;

        // Allineamento iniziale al terreno
        float startHeight = GetHeightAtPosition(transform.position.x, transform.position.z);
        if (startHeight > -50f)
        {
            transform.position = new Vector3(transform.position.x, startHeight, transform.position.z);
            CalculateNextStep();
        }
        else
        {
            Debug.LogError("ERRORE: Agente fuori dal terreno! Spostalo sopra il terreno nella Scena.");
        }
    }

    void FixedUpdate()
    {
        if (hasArrived || !hasTarget) return;
        MoveAgent();
    }

    /// <summary>
    /// Gestisce il movimento fisico verso il target corrente
    /// </summary>
    void MoveAgent()
    {
        if(showDebugGizmos) Debug.DrawLine(transform.position, currentTargetPosition, Color.cyan);

        // Calcolo distanze su piano 2D (ignora altezza per il check di prossimità)
        Vector2 pos2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 target2D = new Vector2(currentTargetPosition.x, currentTargetPosition.z);
        float distToStep = Vector2.Distance(pos2D, target2D);

        // Se non siamo ancora arrivati al passo intermedio
        if (distToStep > 0.05f)
        {
            // 1. Orientamento (ruota solo su asse Y per non inclinarsi)
            Vector3 lookTarget = new Vector3(currentTargetPosition.x, transform.position.y, currentTargetPosition.z);
            transform.LookAt(lookTarget);

            // 2. Scelta velocità (Salita vs Piano)
            float currentSpeed = (currentTargetPosition.y > transform.position.y + 0.05f) ? uphillSpeed : flatSpeed;

            // 3. Movimento Orizzontale
            Vector3 newPos = Vector3.MoveTowards(transform.position, lookTarget, currentSpeed * Time.deltaTime);

            // 4. Adesione al Terreno (Raycast per altezza Y)
            float groundHeight = GetHeightAtPosition(newPos.x, newPos.z);
            if (groundHeight > -50f)
            {
                newPos.y = groundHeight;
            }
            else
            {
                // Fallback gravità se perde il terreno momentaneamente
                newPos.y -= 9.8f * Time.deltaTime;
            }

            transform.position = newPos;
        }
        else
        {
            // Arrivato al passo intermedio: snap preciso e calcolo prossimo
            float finalHeight = GetHeightAtPosition(currentTargetPosition.x, currentTargetPosition.z);
            if (finalHeight > -50f) 
                transform.position = new Vector3(currentTargetPosition.x, finalHeight, currentTargetPosition.z);
            
            CalculateNextStep();
        }
    }

    /// <summary>
    /// Decide la prossima mossa basandosi sulla griglia e sull'acqua
    /// </summary>
    void CalculateNextStep()
    {
        // 1. Controllo Vittoria
        float distToGoal = Vector3.Distance(transform.position, targetLandmark.position);
        
        // Se siamo molto vicini o se l'ultimo target era il landmark stesso
        bool wasTargetingLandmark = hasTarget && Vector3.Distance(currentTargetPosition, targetLandmark.position) < 0.1f;

        if (distToGoal < stopTolerance || wasTargetingLandmark)
        {
            Debug.Log("TRAGUARDO RAGGIUNTO!");
            hasArrived = true;
            enabled = false;
            return;
        }

        // 2. Avvicinamento finale (bypass griglia)
        if (distToGoal < 2.0f)
        {
            currentTargetPosition = targetLandmark.position;
            hasTarget = true;
            return;
        }

        // 3. Calcolo Griglia Standard
        int currentX = Mathf.RoundToInt(transform.position.x - mapOrigin.x);
        int currentZ = Mathf.RoundToInt(transform.position.z - mapOrigin.z);
        Vector2Int gridPos = new Vector2Int(currentX, currentZ);

        Vector3 nextMove = FindBestNeighbor(gridPos);

        // Evita loop su se stesso se non trova strade
        if (Vector3.Distance(nextMove, transform.position) < 0.01f) return;

        currentTargetPosition = nextMove;
        hasTarget = true;
    }

    /// <summary>
    /// Algoritmo Greedy: cerca il vicino sicuro più promettente
    /// </summary>
    Vector3 FindBestNeighbor(Vector2Int currentGridPos)
    {
        Vector2Int[] directions = { 
            new Vector2Int(0, 1),  // Nord
            new Vector2Int(0, -1), // Sud
            new Vector2Int(1, 0),  // Est
            new Vector2Int(-1, 0)  // Ovest
        };

        Vector3 bestPosition = transform.position;
        float bestDistanceToGoal = float.MaxValue;
        float currentWaterLevel = waterObject.position.y;
        
        // Variabili per Panic Mode
        bool foundSafePath = false;
        Vector3 highestPanicPosition = transform.position;
        float maxPanicHeight = -999f;

        foreach (var dir in directions)
        {
            Vector2Int neighborGrid = currentGridPos + dir;

            // Check confini mappa
            if (neighborGrid.x < 0 || neighborGrid.x > mapSize.x || 
                neighborGrid.y < 0 || neighborGrid.y > mapSize.y) continue;

            // Coordinate Mondo
            float worldX = neighborGrid.x + mapOrigin.x;
            float worldZ = neighborGrid.y + mapOrigin.z;

            // Check Altezza Terreno
            float height = GetHeightAtPosition(worldX, worldZ);
            if (height < -50f) continue; // Niente terreno qui

            Vector3 worldPos = new Vector3(worldX, height, worldZ);

            // LOGICA PRINCIPALE
            // È sicuro dall'acqua? (+0.2m margine sicurezza)
            if (height > currentWaterLevel + 0.2f)
            {
                float dist = Vector3.Distance(worldPos, targetLandmark.position);
                if (dist < bestDistanceToGoal)
                {
                    bestDistanceToGoal = dist;
                    bestPosition = worldPos;
                    foundSafePath = true;
                }
            }

            // Tracking per Panic Mode (punto più alto, indipendentemente dal goal)
            if (height > maxPanicHeight)
            {
                maxPanicHeight = height;
                highestPanicPosition = worldPos;
            }
        }

        // Se non c'è strada sicura verso il goal, vai nel punto più alto (Panic Mode)
        if (!foundSafePath) return highestPanicPosition;

        return bestPosition;
    }

    /// <summary>
    /// Helper per ottenere l'altezza del terreno tramite Raycast
    /// </summary>
    float GetHeightAtPosition(float x, float z)
    {
        Ray ray = new Ray(new Vector3(x, 200f, z), Vector3.down);
        RaycastHit hit;

        if (terrainCollider.Raycast(ray, out hit, 300f))
        {
            if(showDebugGizmos) Debug.DrawRay(ray.origin, ray.direction * hit.distance, new Color(0,1,0,0.3f));
            return hit.point.y;
        }
        
        if(showDebugGizmos) Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red);
        return -100f; // Codice errore "Terra non trovata"
    }

    bool ValidateReferences()
    {
        if (targetLandmark == null || waterObject == null || terrainCollider == null)
        {
            Debug.LogError(" ERRORE CONFIGURAZIONE: Controlla di aver assegnato Target, Acqua e Terrain Collider nell'Inspector.");
            enabled = false;
            return false;
        }
        return true;
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.yellow;
        Vector3 center = mapOrigin + new Vector3(mapSize.x / 2f, 5f, mapSize.y / 2f);
        Vector3 size = new Vector3(mapSize.x, 10f, mapSize.y);
        Gizmos.DrawWireCube(center, size);
        
        if (hasTarget)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, currentTargetPosition);
            Gizmos.DrawSphere(currentTargetPosition, 0.2f);
        }
    }
}