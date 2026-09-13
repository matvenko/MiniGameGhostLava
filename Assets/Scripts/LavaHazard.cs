using System.Collections.Generic;
using UnityEngine;
using Sample;

public class LavaHazard : MonoBehaviour
{
    // What burns is the lava pulled back from the floor by Inset, with every
    // corner that points at the floor rounded off by CornerRadius, and it
    // burns once the middle of the character is in it.
    //
    // It used to be the fall itself: the trigger stopped just under a
    // standing character, so it only caught one that had begun to drop - one
    // whose middle had left the floor by about 0.06. Where two pools touch
    // only at a corner that left the middle about 0.09 of room either side of
    // the point between them, and the recordings had people burning there.
    //
    // Falling in still burns, whatever the shape: a character whose middle is
    // more than about 0.17 out over the lava slides off the floor's edge, and
    // once its feet are below the lava's surface it burns there and then. So
    // pulling the shape back further than that gives no more room - the fall
    // gets there first - but it can never leave anyone falling for ever where
    // nothing burns.
    //
    // It is the lava as a whole that is pulled back, not each tile: tile by
    // tile, the seam between two tiles of one pool would be pulled back too,
    // leaving a way into the middle of it. Inset + CornerRadius must not go
    // past half a tile.
    private const float Inset = 0.28f;
    private const float CornerRadius = 0.18f;

    // Tall enough to take in a character standing at full height. The trigger
    // only says when to look; the shape above is what burns.
    public static readonly Vector3 TriggerCenter = new Vector3(0f, .5f, 0f);
    public static readonly Vector3 TriggerSize = new Vector3(1f, 2.2f, 1f);

    private static readonly List<LavaHazard> Active = new List<LavaHazard>();
    private static readonly HashSet<Vector2Int> Cells = new HashSet<Vector2Int>();
    private static Vector2 _origin;
    private static bool _stale = true;

    private void OnEnable()
    {
        Active.Add(this);
        _stale = true;
    }

    private void OnDisable()
    {
        Active.Remove(this);
        _stale = true;
    }

    // A new layout moves tiles about without switching them off and on, so
    // whoever lays it out says so.
    public static void BoardChanged()
    {
        _stale = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Touch(other);
    }

    // Also on stay: a character who is untouchable is set back down on the edge
    // rather than killed, and there are ways to end up inside the lava without
    // ever crossing its edge - teleporting onto it, or a new level laying it
    // down underfoot. Without this they would keep falling. It is also what
    // catches a character already over the tile once it reaches the part that
    // burns, or once it has sunk into it.
    private void OnTriggerStay(Collider other)
    {
        Touch(other);
    }

    private void Touch(Collider other)
    {
        if (!other.CompareTag("Ghost")) return;

        var ghost = other.GetComponentInParent<GhostScript>();
        if (ghost == null) return;

        // The middle of the body the floor holds up, not the transform: the
        // capsule sits a little behind it.
        Vector3 middle;
        float feet;
        if (other is CharacterController cc)
        {
            middle = cc.transform.TransformPoint(cc.center);
            feet = middle.y - cc.height * .5f * Mathf.Abs(cc.transform.lossyScale.y);
        }
        else
        {
            middle = other.bounds.center;
            feet = other.bounds.min.y;
        }

        // The lava's surface is the top of the tile, the same height
        // LiquidSurface draws it at. Standing - even leaning out over an edge
        // as far as the floor will hold - the feet are about 0.2 above it, so
        // only a character that has actually dropped gets this far down.
        float surface = transform.position.y + transform.lossyScale.y * .5f;
        if (feet < surface || Burns(new Vector2(middle.x, middle.z))) ghost.FallIntoLava();
    }

    // The burning shape is the lava pulled back by Inset + CornerRadius and
    // then grown back out by CornerRadius, which is what rounds its outer
    // corners - so a point burns if it is within CornerRadius of the
    // pulled-back lava. Only the tiles around it can be that close.
    private static bool Burns(Vector2 middle)
    {
        if (_stale) Rebuild();

        var at = new Vector2Int(Mathf.RoundToInt(middle.x - _origin.x), Mathf.RoundToInt(middle.y - _origin.y));
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var cell = new Vector2Int(at.x + dx, at.y + dz);
                if (Cells.Contains(cell) && DistanceToPulledBack(middle - _origin - cell, cell) < CornerRadius)
                    return true;
            }
        return false;
    }

    // One tile's share of the pulled-back lava, measured from a point given
    // relative to the tile's middle. Its sides that face anything but lava are
    // drawn in; a corner where the only thing that is not lava is the tile
    // diagonally across has that corner square cut out, so the floor's corner
    // poking in there is given the same room as its sides.
    private static float DistanceToPulledBack(Vector2 u, Vector2Int cell)
    {
        const float pull = Inset + CornerRadius;
        float xMin = Cells.Contains(new Vector2Int(cell.x - 1, cell.y)) ? -.5f : -.5f + pull;
        float xMax = Cells.Contains(new Vector2Int(cell.x + 1, cell.y)) ? .5f : .5f - pull;
        float zMin = Cells.Contains(new Vector2Int(cell.x, cell.y - 1)) ? -.5f : -.5f + pull;
        float zMax = Cells.Contains(new Vector2Int(cell.x, cell.y + 1)) ? .5f : .5f - pull;
        var nearest = new Vector2(Mathf.Clamp(u.x, xMin, xMax), Mathf.Clamp(u.y, zMin, zMax));

        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                if (!Cells.Contains(new Vector2Int(cell.x + sx, cell.y))
                    || !Cells.Contains(new Vector2Int(cell.x, cell.y + sz))
                    || Cells.Contains(new Vector2Int(cell.x + sx, cell.y + sz))) continue;

                float edgeX = sx * (.5f - pull);
                float edgeZ = sz * (.5f - pull);
                if (sx * nearest.x <= sx * edgeX || sz * nearest.y <= sz * edgeZ) continue;

                // The nearest point fell in the cut-out corner: the nearest
                // that is left is on one of its two inner edges.
                return Mathf.Min(Vector2.Distance(u, new Vector2(edgeX, nearest.y)),
                                 Vector2.Distance(u, new Vector2(nearest.x, edgeZ)));
            }
        return Vector2.Distance(u, nearest);
    }

    // Cells counted from whichever lava tile comes first, so it does not
    // matter where on the half-unit the board has been centred this level.
    private static void Rebuild()
    {
        Cells.Clear();
        if (Active.Count > 0)
        {
            Vector3 o = Active[0].transform.position;
            _origin = new Vector2(o.x, o.z);
            foreach (var hazard in Active)
            {
                Vector3 p = hazard.transform.position;
                Cells.Add(new Vector2Int(Mathf.RoundToInt(p.x - _origin.x), Mathf.RoundToInt(p.z - _origin.y)));
            }
        }
        _stale = false;
    }
}
