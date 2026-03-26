namespace RatatuiUnity
{
    /// <summary>
    /// Layout constraint for <see cref="RatatuiTerminal.Split"/>.
    /// Matches the constraint type byte values expected by the Rust C API.
    /// </summary>
    public readonly struct Constraint
    {
        public enum ConstraintType : byte
        {
            Length     = 0,
            Min        = 1,
            Max        = 2,
            Percentage = 3,
            Fill       = 4,
        }

        public readonly ConstraintType Type;
        public readonly ushort Value;

        private Constraint(ConstraintType type, ushort value)
        {
            Type  = type;
            Value = value;
        }

        /// <summary>Fixed number of cells.</summary>
        public static Constraint Length(ushort cells)     => new Constraint(ConstraintType.Length,     cells);

        /// <summary>Minimum number of cells.</summary>
        public static Constraint Min(ushort cells)        => new Constraint(ConstraintType.Min,        cells);

        /// <summary>Maximum number of cells.</summary>
        public static Constraint Max(ushort cells)        => new Constraint(ConstraintType.Max,        cells);

        /// <summary>Percentage of the parent area (0–100).</summary>
        public static Constraint Percentage(ushort pct)   => new Constraint(ConstraintType.Percentage, pct);

        /// <summary>Proportional fill (weight relative to other Fill constraints).</summary>
        public static Constraint Fill(ushort weight = 1)  => new Constraint(ConstraintType.Fill,       weight);
    }
}
