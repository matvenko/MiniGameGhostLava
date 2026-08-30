using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Owns the player's trap inventory (persisted like the coin wallet, so
// traps bought in one run survive into the next) and places them on the
// grid tile the player is standing on.
public class TrapManager : MonoBehaviour
{
    public static TrapManager Instance { get; private set; }

    private const string TrapsOwnedKey = "traps_owned";

    [SerializeField] private GameObject trapPrefab;
    [SerializeField] private Transform player;
    [SerializeField] private Button placeButton;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private float trapHeightOffset = 0.45f;

    private readonly List<Trap> _placed = new List<Trap>();

    public int TrapsOwned { get; private set; }

    void Awake()
    {
        Instance = this;
        TrapsOwned = PlayerPrefs.GetInt(TrapsOwnedKey, 0);
        if (placeButton != null) placeButton.onClick.AddListener(PlaceTrap);
    }

    void Start()
    {
        Refresh();
    }

    public void AddTraps(int amount)
    {
        TrapsOwned += amount;
        Save();
        Refresh();
    }

    public void PlaceTrap()
    {
        if (TrapsOwned <= 0 || trapPrefab == null || player == null) return;

        EnemyPathGrid.Instance.EnsureBuilt();
        Vector3 tile = EnemyPathGrid.Instance.NearestNode(player.position);
        if (HasTrapAt(tile)) return;

        Vector3 pos = new Vector3(tile.x, tile.y + trapHeightOffset, tile.z);
        var go = Instantiate(trapPrefab, pos, Quaternion.identity);
        _placed.Add(go.GetComponent<Trap>());

        TrapsOwned--;
        Save();
        Refresh();
    }

    // Called when the level regenerates: the tiles a trap was armed on may
    // be lava in the new layout, so unfired traps are removed with it. They
    // aren't refunded - placing one already spent it.
    public void ClearPlacedTraps()
    {
        foreach (var trap in _placed)
        {
            if (trap != null) Destroy(trap.gameObject);
        }
        _placed.Clear();
    }

    // Traps destroy themselves after firing, so the list accumulates nulls -
    // clear those out while checking rather than keeping a separate sweep.
    private bool HasTrapAt(Vector3 tile)
    {
        bool occupied = false;
        for (int i = _placed.Count - 1; i >= 0; i--)
        {
            if (_placed[i] == null)
            {
                _placed.RemoveAt(i);
                continue;
            }
            Vector3 p = _placed[i].transform.position;
            if (Mathf.Abs(p.x - tile.x) < 0.5f && Mathf.Abs(p.z - tile.z) < 0.5f) occupied = true;
        }
        return occupied;
    }

    private void Save()
    {
        PlayerPrefs.SetInt(TrapsOwnedKey, TrapsOwned);
        PlayerPrefs.Save();
    }

    public void Refresh()
    {
        if (countText != null) countText.text = TrapsOwned.ToString();
        // The trap's slot in the ability bar. The bar owns the badge on each of
        // its four buttons, so the count is reported to it rather than written
        // straight into the label - which is also why countText is now empty.
        if (AbilityBarUI.Instance != null)
            AbilityBarUI.Instance.SetCount(AbilityBarUI.Ability.Trap, TrapsOwned);
        if (placeButton != null) placeButton.interactable = TrapsOwned > 0;
    }
}
