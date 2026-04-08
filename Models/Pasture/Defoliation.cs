using System;
using System.Linq;
using System.Collections.Generic;
using Models.Core;
using Models.Soils;
using Models.Surface;
using Models.Functions;
using Models.PMF.Interfaces;
using Models.ForageDigestibility;
using Newtonsoft.Json;
using APSIM.Shared.Utilities;
using APSIM.Numerics;
using APSIM.Core;
using System.Drawing.Text;
using CommandLine;

namespace Models.GrazPlan
{   

    /// <summary>
    /// Testing defoliation
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(Zone))]
    [ValidParent(ParentType = typeof(Simulation))]
    public class Defoliation: Model, IStructureDependency
    {
         /// <summary>Structure instance supplied by APSIM.core.</summary>
        [field: NonSerialized]
        public IStructure Structure { private get; set; }

        //[Link] IClock clock = null;
        [Link] ISummary summary = null;
        [Link] Forages forages = null;
        private const double potentialMEOfHerbage = 16.0;

        private List<ZoneWithForage> zones;



         /// <summary>This method is invoked at the beginning of the simulation.</summary>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            if (forages == null)
                throw new Exception("No forages component found in simulation.");
            var parentZone = Parent as Zone;
            if (parentZone == null)
                summary.WriteMessage(this, "When SimpleGrazing is in the top level of the simulation (above the paddocks) it is assumed that the child paddocks are zones within a paddock.",
                                     MessageType.Information);
            double areaOfAllZones = forages.ModelsWithDigestibleBiomass.Select(f => f.Zone)
                                                                       .Distinct()
                                                                       .Sum(z => z.Area);
            zones = forages.ModelsWithDigestibleBiomass.GroupBy(f => f.Zone,
                                                                f => f,
                                                                (z, f) => new ZoneWithForage(this, z, Structure, f.ToList(), areaOfAllZones, summary))
                                                       .ToList();
             // If we are at the top level of the simulation then look in first zone for number of forages.
            int numForages;
            if (Parent is Simulation)
                numForages = zones.First().NumForages;
            else
                numForages = zones.Where(z => z.Zone == this.Parent).First().NumForages;

            speciesCutProportions = MathUtilities.CreateArrayOfValues(1.0, numForages);
            
            
                
        
        } 

        /// <summary>This method is invoked at the beginning of each day to perform management actions.</summary>
        [EventSubscribe("StartOfDay")]
        private void OnStartOfDay(object sender, EventArgs e)
        {
            DaysSinceGraze += 1;
            ProportionOfTotalDM = new double[zones.First().NumForages];
            PostGrazeDM = 0;
            ClippingsWtReturned = 0;
            ClippingsNReturned = 0;
            foreach (var zone in zones)
                zone.OnStartOfDay();
        }

        /// <summary>Number of days since grazing.</summary>
        [JsonIgnore]
        public int DaysSinceGraze { get; private set; }

        /// <summary></summary>
        [JsonIgnore]
        public int GrazingInterval { get; private set; }

         private double[] speciesCutProportions { get; set; }

        /// <summary>DM grazed</summary>
        [JsonIgnore]
        [Units("kgDM/ha")]
        public double GrazedDM => zones.Sum(z => z.GrazedDM);

        /// <summary>N in the DM grazed.</summary>
        [JsonIgnore]
        [Units("kgN/ha")]
        public double GrazedN => zones.Sum(z => z.GrazedN);


        /// <summary></summary>
        [Description("Fraction of clippings returned")]
        public double FractionClippingsReturned { get; set; }

                /// <summary>Mass of clippings returned to soil surface (kg/ha).</summary>
        public double ClippingsWtReturned { get; private set; }

        /// <summary>N in clippings returned to soil surface )(kg N/ha).</summary>
        public double ClippingsNReturned { get; private set; }

        /// <summary>Mass of herbage just after grazing.</summary>
        [JsonIgnore]
        [Units("kgDM/ha")]
        public double PostGrazeDM { get; private set; }

        /// <summary>Proportion of each species biomass to the total biomass.</summary>
        [JsonIgnore]
        [Units("0-1")]
        public double[] ProportionOfTotalDM { get; private set; }

         /// <summary>N in the DM grazed.</summary>
        [JsonIgnore]
        [Units("MJME/ha")]
        public double GrazedME => zones.Sum(z => z.GrazedME);

         /// <summary>Invoked when a grazing occurs.</summary>
        public event EventHandler Grazed;

        /// <summary>
        /// Graze to residue
        /// </summary>
        /// <param name="residual"></param>
        public void GrazeToResidual(double residual)                     
        {
            GrazingInterval = DaysSinceGraze;  // i.e. yesterday's value
            DaysSinceGraze = 0;

            foreach (var zone in zones)
                zone.RemoveDMFromPlants(residual, speciesCutProportions);           

            ClippingsWtReturned = GrazedDM * FractionClippingsReturned;
            ClippingsNReturned = GrazedN * FractionClippingsReturned;
            // foreach (var zone in zones)
            //     zone.AddResidueToSoilSurface(ClippingsWtReturned, ClippingsNReturned, "grass");
            // summary.WriteMessage(this, $"The amount of plant DM added to the soil surface was {ClippingsWtReturned} and the amount of N added was {ClippingsNReturned}", MessageType.Diagnostic);
            
            // Calculate post-grazed dry matter.
            PostGrazeDM = zones.Sum(z => z.TotalDM);

            // Calculate proportions of each species to the total biomass.
            for (int i = 0; i < zones.First().NumForages; i++)
                ProportionOfTotalDM[i] = zones.Select(z => z.ProportionsToTotal[i]).Average();

            summary.WriteMessage(this, string.Format("Grazed {0:0.0} kgDM/ha, N content {1:0.0} kgN/ha, ME {2:0.0} MJME/ha", GrazedDM, GrazedN, GrazedME), MessageType.Diagnostic);
            Grazed?.Invoke(this, new EventArgs());
        }



        private class ZoneWithForage
        {
            private Defoliation defoliation;
            public Zone Zone { get; private set;}
            private List<ModelWithDigestibleBiomass> forages;
            //private IEnumerable<SurfaceOrganicMatter> surfaceOrganicMatters;
            private double areaWeighting;
            private double grazedDM;
            private double grazedN;
            private double grazedME;
            private double dmRemovedToday;
            private ISummary summary;
            public ZoneWithForage(Defoliation defoliation,Zone zone,IStructure structure, List<ModelWithDigestibleBiomass> forages, double areaOfAllZones,ISummary summary)
            {
                this.Zone = zone;
                this.defoliation=defoliation;
                this.forages = forages;
                areaWeighting = zone.Area / areaOfAllZones;
                this.summary =summary;
                
            }
       

            /// <summary>The number of forages in our care</summary>
            public int NumForages => forages.Count;

            /// <summary>Grazed forages</summary>
            public List<Forages.MaterialRemoved> GrazedForages { get; set; } = new();

            /// <summary>Dry matter of all forages in zone, weighted for area on zone (kg/ha)</summary>
            public double TotalDM => forages.Sum(f => f.Material.Sum(m => m.Total.Wt) * 10) * areaWeighting;

            /// <summary>Harvestable dry matter of all forages in zone, weighted for area on zone (kg/ha)</summary>
            public double HarvestableDM => forages.Sum(f => f.Material.Sum(m => m.Consumable.Wt) * 10) * areaWeighting;

            /// <summary>Proportions of each species within the zone to the total dm within the zone (0-1).</summary>
            public List<double> ProportionsToTotal => forages.Select(f => f.Material.Sum(m => m.Total.Wt) / TotalDM).ToList();

            /// <summary>Area weighted grazed dry matter (kg/ha)</summary>
            public double GrazedDM => grazedDM * areaWeighting;

            /// <summary>Area weighted grazed nitrogen (kg N/ha)</summary>
            public double GrazedN => grazedN * areaWeighting;

            /// <summary>Area weighted metabolisable energy in grazed dry matter (kg/ha)</summary>
            public double GrazedME => grazedME * areaWeighting;

            public void OnStartOfDay()
            {
                grazedDM = 0.0;
                grazedN = 0.0;
                grazedME = 0.0;

                GrazedForages.Clear();
            }

            /// <summary>
            /// Reduce the forage population,
            /// </summary>
            /// <param name="fractionPopulationDecline">The fraction to reduce to population to.</param>
            public void ReducePopulation(double fractionPopulationDecline)
            {
                foreach (var forage in forages)
                {
                    if ((forage as IModel) is IHasPopulationReducer populationReducer)
                        populationReducer.ReducePopulation(populationReducer.Population * (1.0 - fractionPopulationDecline));
                    else
                        throw new Exception($"Model {forage.Name} is unable to reduce its population due to grazing. Not implemented.");
                }
            }


            // /// <summary>Remove biomass from the specified forage.</summary>
            // /// <param name="residual">The residual to cut to (kg/ha).</param>
            // /// <param name="speciesCutProportions">The proportions to cut each species.</param>
            // public void RemoveDMFromPlants(double residual, double[] speciesCutProportions)
            // {
            //     // This is a simple implementation. It proportionally removes biomass from organs.
            //     // What about non harvestable biomass?
            //     // What about PreferenceForGreenOverDead and PreferenceForLeafOverStems?
            //     double preGrazeDM = forages.Sum(f => f.Material.Sum(m => m.Total.Wt * 10));
            //     double removeAmount = Math.Max(0, preGrazeDM - residual) / 10; // to g/m2

            //     dmRemovedToday = removeAmount;
            //     if (MathUtilities.IsGreaterThan(removeAmount, 0.0))
            //     {
            //         // Remove a proportion of required DM from each species
            //         double totalHarvestableWt = 0.0;
            //         double totalWeightedHarvestableWt = 0.0;
            //         for (int i = 0; i < forages.Count; i++)
            //         {
            //             var harvestableWt = forages[i].Material.Sum(m => m.Consumable.Wt);  // g/m2
            //             totalHarvestableWt += harvestableWt;
            //             totalWeightedHarvestableWt += speciesCutProportions[i] * harvestableWt;
            //         }

            //         // If a fraction consumable was specified in the forages component by the user then the above calculated
            //         // removeAmount might be > consumable amount. Constrain the removeAmount to the consumable
            //         // amount so that we don't get an exception thrown in ModelWithDigestibleBiomass.RemoveBiomass method
            //         removeAmount = Math.Min(removeAmount, totalHarvestableWt);

            //         for (int i = 0; i < forages.Count; i++)
            //         {
            //             var harvestableWt = forages[i].Material.Sum(m => m.Consumable.Wt);  // g/m2
            //             var proportion = harvestableWt * speciesCutProportions[i] / totalWeightedHarvestableWt;
            //             var amountToRemove = removeAmount * proportion;
            //             if (MathUtilities.IsGreaterThan(amountToRemove, 0.0))
            //             {
            //                 var amountToRemoveKgHa = amountToRemove * 10.0; // g/m2 → kg/ha
                            
            //                 var grazed = forages[i].RemoveBiomass(amountToRemove: amountToRemoveKgHa);
                            
                            

            //                 double grazedDigestibility = grazed.Digestibility;
            //                 var grazedMetabolisableEnergy = potentialMEOfHerbage * grazedDigestibility;
                           
            //                 grazedDM += grazed.Wt;  // kg/ha
            //                 grazedN += grazed.N;    // kg/ha
            //                 grazedME += grazedMetabolisableEnergy * grazed.Wt;

            //                 GrazedForages.Add(grazed);
            //             }
            //         }
            //     }
            // }


             /// <summary>Remove biomass from the specified forage.</summary>
            /// <param name="residual">The residual to cut to (kg/ha).</param>
            /// <param name="speciesCutProportions">The proportions to cut each species.</param>
            public void RemoveDMFromPlants(double residual, double[] speciesCutProportions)
            {
                // This is a simple implementation. It proportionally removes biomass from organs.
                // What about non harvestable biomass?
                // What about PreferenceForGreenOverDead and PreferenceForLeafOverStems?
                double preGrazeDM = forages.Sum(f => f.Material.Sum(m => m.Total.Wt * 10));
                double removeAmount = Math.Max(0, preGrazeDM - residual) / 10; // to g/m2

                dmRemovedToday = removeAmount;
                if (MathUtilities.IsGreaterThan(removeAmount, 0.0))
                {
                    // Remove a proportion of required DM from each species
                    double totalHarvestableWt = 0.0;
                    double totalWeightedHarvestableWt = 0.0;
                    for (int i = 0; i < forages.Count; i++)
                    {
                        var harvestableWt = forages[i].Material.Sum(m => m.Consumable.Wt);  // g/m2
                        totalHarvestableWt += harvestableWt;
                        totalWeightedHarvestableWt += speciesCutProportions[i] * harvestableWt;
                    }

                    // If a fraction consumable was specified in the forages component by the user then the above calculated
                    // removeAmount might be > consumable amount. Constrain the removeAmount to the consumable
                    // amount so that we don't get an exception thrown in ModelWithDigestibleBiomass.RemoveBiomass method
                    removeAmount = Math.Min(removeAmount, totalHarvestableWt);

                    for (int i = 0; i < forages.Count; i++)
                    {
                        var harvestableWt = forages[i].Material.Sum(m => m.Consumable.Wt);  // g/m2
                        var proportion = harvestableWt * speciesCutProportions[i] / totalWeightedHarvestableWt;
                        var amountToRemove = removeAmount * proportion;
                        if (MathUtilities.IsGreaterThan(amountToRemove, 0.0))
                        {
                           
                             var grazed = forages[i].RemoveBiomass(
                             amountToRemove,
                             PreferenceForGreenOverDead: 1.0,
                             PreferenceForLeafOverStems: 1.0,
                             summary: summary);
                          
                            double grazedDigestibility = grazed.Digestibility;
                            var grazedMetabolisableEnergy = potentialMEOfHerbage * grazedDigestibility;

                            grazedDM += grazed.Wt;  // kg/ha
                            grazedN += grazed.N;    // kg/ha
                            grazedME += grazedMetabolisableEnergy * grazed.Wt;

                            GrazedForages.Add(grazed);
                        }
                    }
                }
            }



                                 
        }                            

    }
}