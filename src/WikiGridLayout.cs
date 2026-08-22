namespace Morgott.Oracle
{
    /// <summary>The resolved shape of one wiki grid: how many columns/rows, at what cell size.</summary>
    public struct WikiGridPlan
    {
        public int Columns;
        public int Rows;
        public float CellSize;
        public float Width;
        public float Height;
    }

    /// <summary>
    /// Pure layout math for the wiki grids: given a LIVE cell count and the area actually available on
    /// screen, decide columns / rows / cell size. Engine-free (no Unity types) so it unit-tests under net8.
    ///
    /// <para>Rule, in order — the first shape that fits wins:</para>
    /// <list type="number">
    /// <item>the preferred shape (<paramref name="preferredColumns"/> at the measured native cell size),
    /// clamped to what the width can hold — so a vanilla-sized pool renders exactly as it always did;</item>
    /// <item>widen to every column the width can hold, trading rows for columns;</item>
    /// <item>shrink the cell (binary search) down to <see cref="MinCellFraction"/> of the native size,
    /// re-widening as cells get smaller.</item>
    /// </list>
    /// <para>Past that the count is simply too large for the screen: the plan stays at the minimum cell
    /// size and the caller's ScrollRect takes over. Nothing here assumes a maximum count — TFTV can add
    /// drills at any time.</para>
    /// </summary>
    public static class WikiGridLayout
    {
        /// <summary>Never shrink a cell below this fraction of the native size (icons stay readable).</summary>
        public const float MinCellFraction = 0.5f;

        private const int SearchSteps = 20;

        public static WikiGridPlan Plan(int count, float preferredCell, float spacing, int preferredColumns,
            float availableWidth, float availableHeight)
        {
            if (count <= 0 || preferredCell <= 0f)
            {
                return default(WikiGridPlan);
            }
            if (spacing < 0f)
            {
                spacing = 0f;
            }
            if (preferredColumns < 1)
            {
                preferredColumns = 1;
            }

            // No measurable area (asked before the canvas laid out): keep the preferred shape verbatim.
            if (availableWidth <= 0f || availableHeight <= 0f)
            {
                return Shape(count, Min(count, preferredColumns), preferredCell, spacing);
            }

            int preferredFit = Min(preferredColumns, ColumnsForWidth(preferredCell, spacing, availableWidth));
            WikiGridPlan plan = Shape(count, Min(count, preferredFit), preferredCell, spacing);
            if (plan.Height <= availableHeight)
            {
                return plan;
            }

            int wideFit = Min(count, ColumnsForWidth(preferredCell, spacing, availableWidth));
            if (wideFit > plan.Columns)
            {
                plan = Shape(count, wideFit, preferredCell, spacing);
                if (plan.Height <= availableHeight)
                {
                    return plan;
                }
            }

            // Largest cell size that still fits the height; falls back to the smallest allowed size.
            float low = preferredCell * MinCellFraction;
            float high = preferredCell;
            WikiGridPlan best = Fit(count, low, spacing, availableWidth);
            for (int i = 0; i < SearchSteps; i++)
            {
                float mid = (low + high) * 0.5f;
                WikiGridPlan candidate = Fit(count, mid, spacing, availableWidth);
                if (candidate.Height <= availableHeight)
                {
                    best = candidate;
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }
            return best;
        }

        /// <summary>The widest shape a given cell size can take inside <paramref name="availableWidth"/>.</summary>
        private static WikiGridPlan Fit(int count, float cell, float spacing, float availableWidth)
        {
            return Shape(count, Min(count, ColumnsForWidth(cell, spacing, availableWidth)), cell, spacing);
        }

        private static int ColumnsForWidth(float cell, float spacing, float availableWidth)
        {
            int columns = (int)((availableWidth + spacing) / (cell + spacing));
            return columns < 1 ? 1 : columns;
        }

        private static WikiGridPlan Shape(int count, int columns, float cell, float spacing)
        {
            if (columns < 1)
            {
                columns = 1;
            }
            int rows = (count + columns - 1) / columns;
            return new WikiGridPlan
            {
                Columns = columns,
                Rows = rows,
                CellSize = cell,
                Width = columns * cell + (columns - 1) * spacing,
                Height = rows * cell + (rows - 1) * spacing,
            };
        }

        private static int Min(int a, int b)
        {
            return a < b ? a : b;
        }
    }
}
