using System;

namespace SpaceSim.Simulation.Orbits
{
    /// <summary>
    /// Solves Kepler's equation: M = E - e * sin(E)
    /// using Newton-Raphson iteration.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    ///
    /// For circular orbits (e == 0), E == M exactly (no iteration needed).
    /// For elliptical orbits (0 < e < 1), converges in 3-6 iterations typically.
    ///
    /// Performance: zero allocations, no LINQ, small fixed iteration count.
    /// </summary>
    public static class KeplerSolver
    {
        /// <summary>Maximum Newton-Raphson iterations.</summary>
        private const int MaxIterations = 8;

        /// <summary>Convergence threshold in radians.</summary>
        private const double ConvergenceThreshold = 1e-10;

        /// <summary>
        /// Solve Kepler's equation for eccentric anomaly E given mean anomaly M
        /// and eccentricity e.
        ///
        /// M = E - e * sin(E)
        ///
        /// Newton-Raphson: E_{n+1} = E_n - (E_n - e*sin(E_n) - M) / (1 - e*cos(E_n))
        /// </summary>
        /// <param name="meanAnomaly">Mean anomaly in radians.</param>
        /// <param name="eccentricity">Orbital eccentricity [0, 1).</param>
        /// <returns>Eccentric anomaly E in radians.</returns>
        public static double Solve(double meanAnomaly, double eccentricity)
        {
            // Circular orbit shortcut — no iteration needed.
            if (eccentricity < 1e-12)
                return meanAnomaly;

            // Clamp eccentricity to valid range for elliptical orbits.
            double e = eccentricity;
            if (e >= 1.0) e = 0.999;
            if (e < 0.0) e = 0.0;

            // Normalize M to [0, 2*PI).
            double M = meanAnomaly % (2.0 * Math.PI);
            if (M < 0.0) M += 2.0 * Math.PI;

            // Initial guess: E = M + e * sin(M) (good for low eccentricity).
            // For higher eccentricity, a better initial guess improves convergence.
            double E = M + e * Math.Sin(M);

            // Newton-Raphson iteration.
            for (int i = 0; i < MaxIterations; i++)
            {
                double sinE = Math.Sin(E);
                double cosE = Math.Cos(E);

                double f = E - e * sinE - M;
                double fPrime = 1.0 - e * cosE;

                // Guard against near-zero derivative (extremely unlikely for e < 1).
                if (Math.Abs(fPrime) < 1e-15)
                    break;

                double delta = f / fPrime;
                E -= delta;

                if (Math.Abs(delta) < ConvergenceThreshold)
                    break;
            }

            return E;
        }
    }
}
