using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static KSP_PLUGIN.Structs;
using static Orbit;

namespace KSP_PLUGIN
{
    class OrbUtil
    {
        public static OrbitData GetOrbitData(Orbit o)
        {
            double anom = (o.trueAnomaly > Math.PI) ? (o.trueAnomaly - Math.PI * 2) : o.trueAnomaly;
            double anomEnd = o.TrueAnomalyAtUT(o.EndUT);
            if (o.patchEndTransition == PatchTransitionType.FINAL)
            {
                anomEnd = o.trueAnomaly + (2 * Math.PI);
            }
            else //angles are weird
            {
                if (anomEnd < anom)
                {
                    while (anomEnd < anom)
                    {
                        anomEnd += Math.PI * 2;
                    }
                }
                else
                {
                    while (anomEnd > anom)
                    {
                        anomEnd -= Math.PI * 2;
                    }
                    anomEnd += Math.PI * 2;
                }
            }
            /* double E = o.GetEccentricAnomaly(-o.argumentOfPeriapsis * Deg2Rad);
             double AN_T = AddPWhileNegative((AddPWhileNegative(o.getObTAtMeanAnomaly(E - (o.eccentricity * Math.Sin(E))), o.period) - AddPWhileNegative(o.ObT, o.period)), o.period);
             E = o.GetEccentricAnomaly(-o.argumentOfPeriapsis * Deg2Rad + Math.PI);
             double DN_T = AddPWhileNegative((AddPWhileNegative(o.getObTAtMeanAnomaly(E - (o.eccentricity * Math.Sin(E))), o.period) - AddPWhileNegative(o.ObT, o.period)), o.period);
             */

            return new OrbitData()
            {
                SOINumber = Util.GetSOINumber(o.referenceBody.name),
                longOfAscNode = o.LAN,
                argOfPE = o.argumentOfPeriapsis,
                SemiLatusRectum = o.semiLatusRectum,
                e = o.eccentricity,
                inc = (float)o.inclination,
                anomoly = anom,
                anomolyEnd = anomEnd,
                AP = (float)o.ApA,
                PE = (float)o.PeA,
                T2Pe = (int)o.timeToPe,
                T2AN = (int)T2TAnom(o, -o.argumentOfPeriapsis * Deg2Rad),
                T2DN = (int)T2TAnom(o, -o.argumentOfPeriapsis * Deg2Rad + Math.PI),
                //T2AN = (int)(AN_T),
                //T2DN = (int)(DN_T),
                period = (int)o.period,
                T2PatchEnd = (int)(o.EndUT - Planetarium.GetUniversalTime()),
                transStart = (byte)o.patchStartTransition,
                transEnd = (byte)o.patchEndTransition,
            };
        }

        public static double T2TAnom(Orbit o, double tA)
        {
            double E = o.GetEccentricAnomaly(tA);
            return AddPWhileNegative((AddPWhileNegative(o.getObTAtMeanAnomaly(E - (o.eccentricity * Math.Sin(E))), o.period) - AddPWhileNegative(o.ObT, o.period)), o.period);
        }

        public static double AddPWhileNegative(double t, double p)
        {
            while (t < 0)
            {
                t += p;
            }
            return t;
        }
        
        //stolen from krpc
        /// <summary>
        /// Helper function to calculate the closest approach distance and time to a target orbit
        /// in a given orbital period.
        /// </summary>
        /// <param name="myOrbit">Orbit of the controlled vessel.</param>
        /// <param name="targetOrbit">Orbit of the target.</param>
        /// <param name="beginTime">Time to begin search, which continues for
        /// one orbital period from this time.</param>
        /// <param name="distance">The distance at the closest approach, in meters.</param>
        /// <returns>The universal time at closest approach, in seconds.</returns>
        public static double CalcClosestAproach(Orbit myOrbit, Orbit targetOrbit, double beginTime, out double distance)
        {
            double approachTime = beginTime;
            double approachDistance = double.MaxValue;
            double mintime = beginTime;
            double interval = myOrbit.period;
            if (myOrbit.eccentricity > 1.0)
                interval = 100 / myOrbit.meanMotion;
            double maxtime = mintime + interval;

            // Conduct coarse search
            double timestep = (maxtime - mintime) / 20;
            double placeholder = mintime;
            while (placeholder < maxtime)
            {
                Vector3d PosA = myOrbit.getPositionAtUT(placeholder);
                Vector3d PosB = targetOrbit.getPositionAtUT(placeholder);
                double thisDistance = Vector3d.Distance(PosA, PosB);
                if (thisDistance < approachDistance)
                {
                    approachDistance = thisDistance;
                    approachTime = placeholder;
                }
                placeholder += timestep;
            }

            // Conduct fine search
            double fine_mintime = approachTime - timestep;
            double fine_maxtime = approachTime + timestep;
            if (fine_maxtime > maxtime) fine_maxtime = maxtime;
            if (fine_mintime < mintime) fine_mintime = mintime;
            timestep = (fine_maxtime - fine_mintime) / 100;
            placeholder = fine_mintime;

            while (placeholder < fine_maxtime)
            {
                Vector3d PosA = myOrbit.getPositionAtUT(placeholder);
                Vector3d PosB = targetOrbit.getPositionAtUT(placeholder);
                double thisDistance = Vector3d.Distance(PosA, PosB);
                if (thisDistance < approachDistance)
                {
                    approachDistance = thisDistance;
                    approachTime = placeholder;
                }
                placeholder += timestep;
            }
            distance = approachDistance;
            return approachTime;
        }
    }
}
