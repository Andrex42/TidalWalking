using UnityEngine;
using System.Collections;

public class WaterPlaneController : MonoBehaviour
{
    [Header("Terrain (leave empty to auto-detect)")]
    public Terrain terrain;

    [Header("Water level")]
    public float minHeight = 0.5f;
    public float maxHeight = 7f;
    public float speed = 0.5f;  // m/s

    private void Start()
    {
        // trova il terrain se non assegnato
        if (!terrain)
            terrain = FindObjectOfType<Terrain>();

        if (!terrain)
        {
            Debug.LogError("Nessun Terrain trovato in scena!");
            return;
        }

        // scala e centra il plane
        ScaleAndCenterPlane();

        // imposta l'altezza iniziale
        Vector3 pos = transform.position;
        pos.y = minHeight;
        transform.position = pos;

        // avvia il loop dell'acqua
        StartCoroutine(WaterLevelLoop());
    }

    private void ScaleAndCenterPlane()
    {
        TerrainData td = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = td.size;

        // Unity Plane è 10x10 unit → scala = size/10
        float scaleX = tSize.x / 10f;
        float scaleZ = tSize.z / 10f;

        transform.localScale = new Vector3(scaleX, 1f, scaleZ);

        // centra il plane sul terrain
        transform.position = new Vector3(
            tPos.x + tSize.x * 0.5f,
            transform.position.y,
            tPos.z + tSize.z * 0.5f
        );
    }

    private IEnumerator WaterLevelLoop()
    {
        float current = minHeight;
        bool goingUp = true;

        while (true)
        {
            if (goingUp)
            {
                current += speed * Time.deltaTime;

                if (current >= maxHeight)
                {
                    current = maxHeight;
                    goingUp = false;
                }
            }
            else
            {
                current -= speed * Time.deltaTime;

                if (current <= minHeight)
                {
                    current = minHeight;
                    goingUp = true;
                }
            }

            // applica altezza
            Vector3 pos = transform.position;
            pos.y = current;
            transform.position = pos;

            yield return null;
        }
    }
}
