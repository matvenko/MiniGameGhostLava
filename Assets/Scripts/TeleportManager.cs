using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sample;

// Owns the player's stock of teleport charges (persisted like the trap stock
// and the freeze charges, so charges bought in one run survive into the next)
// and spends one to put the character down somewhere else on the board.
//
// The jump itself is the respawn move - the character is picked up and set on
// a walkable tile, which is what the debug Space key has always done - only
// the tile is chosen rather than being the one it started on. Chosen means
// safe: never lava, since only Blocks tiles are candidates, and no enemy
// within safeRadius of it, so the jump can't drop the player in front of
// whatever it was running from.
//
// If the board is so crowded that nothing is that clear - a small level with
// several enemies spread across it - the ability still fires, on whichever
// tile has the most room around it. Landing somewhere merely better is worth
// more to the player than a button that quietly refuses.
public class TeleportManager : MonoBehaviour
{
    public static TeleportManager Instance { get; private set; }

    private const string TeleportsOwnedKey = "teleports_owned";

    [SerializeField] private Button useButton;
    [SerializeField] private GhostScript player;
    [Tooltip("How many cells clear of every enemy a tile has to be to count as safe.")]
    [SerializeField] private float safeRadius = 7.5f;
    [Tooltip("And how far the jump has to carry, so a charge is never spent on the spot the player is already standing on.")]
    [SerializeField] private float minJumpDistance = 5f;

    public int TeleportsOwned { get; private set; }

    void Awake()
    {
        Instance = this;
        TeleportsOwned = PlayerPrefs.GetInt(TeleportsOwnedKey, 0);
        if (useButton != null) useButton.onClick.AddListener(UseTeleport);
    }

    void Start()
    {
        Refresh();
    }

    public void AddTeleports(int amount)
    {
        TeleportsOwned += amount;
        Save();
        Refresh();
    }

    public void UseTeleport()
    {
        if (TeleportsOwned <= 0 || player == null) return;
        if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive) return;
        if (LevelManager.Instance != null && LevelManager.Instance.IsLevelCompleteActive) return;

        if (!TryFindDestination(out Vector3 destination)) return;

        player.TeleportTo(destination);
        AudioManager.Play(GameSound.Teleport);

        TeleportsOwned--;
        Save();
        Refresh();
    }

    // Walks the walkable tiles once, keeping every one that is far enough from
    // both the enemies and the player, and remembering the roomiest tile seen
    // in case none of them is. A random pick out of the safe set is what makes
    // the landing spot different every time rather than always the far corner.
    private bool TryFindDestination(out Vector3 destination)
    {
        destination = default;

        EnemyPathGrid.Instance.EnsureBuilt();
        var tiles = EnemyPathGrid.Instance.AllNodes;
        if (tiles.Count == 0) return false;

        Vector3 from = player.transform.position;
        var safe = new List<Vector3>();
        Vector3 roomiest = default;
        float roomiestClearance = float.MinValue;
        bool haveRoomiest = false;

        for (int i = 0; i < tiles.Count; i++)
        {
            Vector3 tile = tiles[i];
            if (PlanarDistance(tile, from) < minJumpDistance) continue;

            float clearance = ClearanceOf(tile);
            if (clearance >= safeRadius)
            {
                safe.Add(tile);
            }
            else if (clearance > roomiestClearance)
            {
                roomiestClearance = clearance;
                roomiest = tile;
                haveRoomiest = true;
            }
        }

        if (safe.Count > 0)
        {
            destination = safe[Random.Range(0, safe.Count)];
            return true;
        }
        if (haveRoomiest)
        {
            destination = roomiest;
            return true;
        }
        return false;
    }

    // Distance from a tile to the nearest enemy. A board with no enemies on it
    // yet - the portal warm-up - leaves every tile equally clear, which is the
    // right answer: anywhere is safe.
    private static float ClearanceOf(Vector3 tile)
    {
        float nearest = float.MaxValue;
        var enemies = EnemyChaser.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null) continue;
            float d = PlanarDistance(tile, enemy.transform.position);
            if (d < nearest) nearest = d;
        }
        return nearest;
    }

    // Tiles and characters sit at different heights, and the height has
    // nothing to say about whether an enemy can reach you - only the floor
    // distance does.
    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(TeleportsOwnedKey, TeleportsOwned);
        PlayerPrefs.Save();
    }

    public void Refresh()
    {
        // The bar owns the badge on the teleport button, so the count goes to
        // it rather than into a label of our own.
        if (AbilityBarUI.Instance != null)
            AbilityBarUI.Instance.SetCount(AbilityBarUI.Ability.Teleport, TeleportsOwned);
        if (useButton != null) useButton.interactable = TeleportsOwned > 0;
    }
}
