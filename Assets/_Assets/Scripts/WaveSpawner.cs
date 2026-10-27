using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class WaveEntry
{
    public GameObject prefab;
    public int count = 1;
}

[Serializable]
public class Wave
{
    public string label = "Wave";
    public WaveEntry[] entries;
    public float spawnInterval = 0.6f;
}

public class WaveSpawner : MonoBehaviour
{
    public enum Phase { Waiting, Spawning, Fighting, Finished }

    [SerializeField] Wave[] waves;
    [SerializeField] bool autoStart = true;
    [SerializeField] float startDelay = 4f;
    [SerializeField] float betweenWaves = 5f;

    [Header("Spawn Area (around the player)")]
    [SerializeField] float minRadius = 22f;
    [SerializeField] float maxRadius = 32f;
    [SerializeField] float flyingHeight = 4f;
    [SerializeField] LayerMask groundMask = ~0;

    readonly List<GameObject> alive = new List<GameObject>();
    readonly Queue<GameObject> queue = new Queue<GameObject>();
    Transform player;
    Health playerHealth;
    Phase phase = Phase.Waiting;
    int index = -1;
    float nextTime;
    float nextSpawnTime;

    public Phase CurrentPhase => phase;
    public int WaveNumber => Mathf.Max(index + 1, 0);
    public int TotalWaves => waves != null ? waves.Length : 0;
    public int AliveCount => alive.Count;
    public float TimeToNextWave => Mathf.Max(0f, nextTime - Time.time);

    // text for the HUD
    public string StatusText
    {
        get
        {
            switch (phase)
            {
                case Phase.Waiting:
                    return index + 1 >= TotalWaves ? "" : "Wave " + (index + 2) + " in " + Mathf.CeilToInt(TimeToNextWave);
                case Phase.Finished: return "ALL WAVES CLEARED";
                default: return "Wave " + WaveNumber + " / " + TotalWaves + "    Enemies: " + (alive.Count + queue.Count);
            }
        }
    }

    void Start()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) { player = go.transform; playerHealth = go.GetComponentInParent<Health>(); }
        nextTime = Time.time + startDelay;
        if (!autoStart) enabled = false;
    }

    public void StartWaves()
    {
        enabled = true;
        nextTime = Time.time + startDelay;
    }

    void Update()
    {
        if (player == null || (playerHealth != null && playerHealth.IsDead)) return;

        alive.RemoveAll(e => e == null || (e.TryGetComponent(out Health h) && h.IsDead));

        switch (phase)
        {
            case Phase.Waiting:
                if (Time.time >= nextTime) BeginWave();
                break;

            case Phase.Spawning:
                if (Time.time >= nextSpawnTime && queue.Count > 0)
                {
                    var prefab = queue.Dequeue();
                    if (SpawnOne(prefab)) nextSpawnTime = Time.time + waves[index].spawnInterval;
                    else { queue.Enqueue(prefab); nextSpawnTime = Time.time + 1f; }   // no valid spot right now, try again shortly
                }
                if (queue.Count == 0) phase = Phase.Fighting;
                break;

            case Phase.Fighting:
                if (alive.Count == 0)
                {
                    if (index + 1 >= waves.Length) phase = Phase.Finished;
                    else { phase = Phase.Waiting; nextTime = Time.time + betweenWaves; }
                }
                break;
        }
    }

    void BeginWave()
    {
        index++;
        if (waves == null || index >= waves.Length) { phase = Phase.Finished; return; }

        queue.Clear();
        foreach (var e in waves[index].entries)
            for (int i = 0; i < e.count; i++)
                if (e.prefab != null) queue.Enqueue(e.prefab);

        phase = Phase.Spawning;
        nextSpawnTime = Time.time;
        SpellFX.Play(Sfx.Wave, player.position);
    }

    // returns false if no valid spawn spot was found (nothing is spawned in that case)
    bool SpawnOne(GameObject prefab)
    {
        bool grounded = prefab.GetComponent<NavMeshAgent>() != null;
        Vector3 pos = Vector3.zero;
        bool found = false;

        for (int attempt = 0; attempt < 16 && !found; attempt++)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector3 p = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * UnityEngine.Random.Range(minRadius, maxRadius);
            p = OnGround(p);   // the player may be flying, so look for the ground rather than their height

            if (grounded)
            {
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 12f, NavMesh.AllAreas)) continue;
                pos = hit.position;
            }
            else
            {
                pos = p + Vector3.up * flyingHeight;
            }
            found = true;
        }
        if (!found) return false;

        Vector3 toPlayer = player.position - pos;
        toPlayer.y = 0f;
        var rot = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

        // an agent's pivot sits above its feet by the base offset, so lift the spawn point or the agent can't find the NavMesh
        Vector3 navPos = pos;
        var prefabAgent = prefab.GetComponent<NavMeshAgent>();
        if (prefabAgent != null) pos += Vector3.up * prefabAgent.baseOffset * prefab.transform.lossyScale.y;

        var go = Instantiate(prefab, pos, rot);
        if (go.TryGetComponent(out NavMeshAgent agent) && agent.enabled && !agent.isOnNavMesh) agent.Warp(navPos);
        go.SendMessage("Alert", SendMessageOptions.DontRequireReceiver);
        alive.Add(go);
        return true;
    }

    // drops a point straight down onto the ground below it
    Vector3 OnGround(Vector3 p)
    {
        return Physics.Raycast(new Vector3(p.x, 500f, p.z), Vector3.down, out RaycastHit g, 1000f, groundMask, QueryTriggerInteraction.Ignore)
            ? g.point
            : new Vector3(p.x, transform.position.y, p.z);
    }
}
