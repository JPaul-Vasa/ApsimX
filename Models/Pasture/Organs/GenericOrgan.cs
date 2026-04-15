using APSIM.Numerics;
using APSIM.Shared.Utilities;
using Models.Core;

using Models.Interfaces;
using Models.Soils;
using Models.Soils.Arbitrator;
using Models.Soils.Nutrients;
using Models.Surface;
using Newtonsoft.Json;
using StdUnits;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using static Models.GrazPlan.GrazType;
using static Models.GrazPlan.PastureUtil;
using APSIM.Core;
using Models.PMF.Library;
using Models.PMF.Interfaces;
using Models.PMF;
using Models.PMF.Organs;

using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Models.GrazPlan.Organs
{

    /// <summary>This is a Organ class with Leaf, Stem and Root. It can be extended to other organs. Currently calculates DM,N and NConc.</summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(Pasture))]
    
    public class GenericOrgan: Model,IStructureDependency,IBiomass,IOrganDamage
    {   
        /// <summary>Structure instance supplied by APSIM.core.</summary>
        [field: NonSerialized]
        public IStructure Structure { private get; set; }

        


         /// <summary>Gets a value indicating whether the biomass is above ground or not</summary>
        [Description("Is organ above ground?")]
        public bool IsAboveGround { get; set; }


       
        /// <summary>
        /// TPasturePopulation
        /// </summary>
        public TPasturePopulation PastureModel;
        private double GetDM(int comp, int part)
        {   
    
            string sUnit = PastureModel.MassUnit;
            PastureModel.MassUnit = "kg/ha";
            double result = PastureModel.GetHerbageMass(comp, part, GrazType.TOTAL);
            PastureModel.MassUnit = sUnit;
            return result;
        }

        
        /// <summary>
        /// Get average nutrient content of a plant (g/g) (CONCENTRATION NOT AMT)
        /// </summary>
        /// <param name="comp">Herbage</param>
        /// <param name="part">Plant part</param>
        /// <param name="elem">Nutrient element</param>
        /// <returns></returns>
        private double GetPlantNutr(int comp, int part, TPlantElement elem)
        {
            return PastureModel.GetHerbageConc(comp, part, GrazType.TOTAL, elem);
        }

        private double GetDMRoot()
        {
            string sUnit = PastureModel.MassUnit;
            PastureModel.MassUnit = "kg/ha";
            double result = PastureModel.GetRootMass(GrazType.sgGREEN, GrazType.TOTAL, GrazType.TOTAL);
            //double result = PastureModel.GetRootMass(GrazType.ptROOT, GrazType.TOTAL, GrazType.TOTAL);
            PastureModel.MassUnit = sUnit;
            return result;
             
        }

        /// <summary>
        /// Get the average digestibility of this herbage
        /// </summary>
        /// <param name="comp">Herbage component</param>
        /// <param name="part">Plant part</param>
        /// <returns></returns>
        private double GetDMD(int comp, int part)
        {
            string sUnit = PastureModel.MassUnit;
            PastureModel.MassUnit = "kg/ha";
            double result = PastureModel.Digestibility(comp, part);
            PastureModel.MassUnit = sUnit;

            return result;
        }
        

        /// <summary>
        /// StructuralWt of the Organ Live+ Dead
        /// </summary>
        [JsonIgnore]
        [Units("g/m^2")]

        public double StructuralWt
        {
            get
            {   

                if (PastureModel != null)
                {
                    if(Name=="Leaf" && IsAboveGround is true)
                        return GetDM(GrazType.TOTAL, GrazType.ptLEAF)/10.0;
                    if(Name=="Stem" && IsAboveGround is true)
                        return GetDM(GrazType.TOTAL, GrazType.ptSTEM)/10.0;

                    if (Name == "Root" && IsAboveGround is false)
                    {
                        return GetDMRoot()/10.0;
                    }
                    
                }
      
                return 0;
            }
        }


         /// <summary>
        /// StorageWt in Organ Live+ Dead
        /// </summary>
        public double StorageWt
        {
            get
            {
                return 0;
            }
        }

        /// <summary>
        /// StorageN of Organ Live+ Dead
        /// </summary>
        public double StorageN
        {
            get
            {
                return 0;
            }
        }

         /// <summary>
        /// Nitrogen content of Organ Live+ Dead
        /// </summary>
        public double StructuralN
        {
            get
            {   
                if (PastureModel != null)
                {
                    if(Name=="Leaf"  && IsAboveGround is true)
                        return GetDM(GrazType.TOTAL, GrazType.ptLEAF)/10.0 * GetPlantNutr(GrazType.TOTAL, GrazType.ptLEAF, TPlantElement.N);
                    if(Name=="Stem"  && IsAboveGround is true)
                        return GetDM(GrazType.TOTAL, GrazType.ptSTEM)/10.0 * GetPlantNutr(GrazType.TOTAL, GrazType.ptSTEM, TPlantElement.N);
                    if (Name == "Root" && IsAboveGround is false)
                    {
                        return  GetDMRoot()/10.0 * PastureModel.GetRootConc(GrazType.sgGREEN, GrazType.TOTAL, GrazType.TOTAL, TPlantElement.N);
                    }
                }
                
                return 0;
            }
        }


        /// <summary>
        /// DM of Organ Live+ Dead
        /// </summary>
        public double Wt
        {
            get
            {
                return StructuralWt+StorageWt;
            }
        }

        

        /// <summary>
        /// N amount of Organ Live+ Dead
        /// </summary>
        public double N
        {
            get
            {
                return StructuralN + StorageN;
            }
        }

        /// <summary>
        /// N concentration of Organ Live+ Dead
        /// </summary>
        public double NConc
        {
            get
            {   
                if (Wt > 0)
                {
                    return N/Wt;
                }

                return 0;
                

            }
        }

        
        private PMF.Biomass liveBiomass = new PMF.Biomass();
         private PMF.Biomass deadBiomass = new PMF.Biomass();
         /// <summary>Gets the cohort live.</summary>
        [JsonIgnore]
        [Units("g/m^2")]
        public PMF.Biomass Live
        {
            get
            {
                CalculateLiveDead();
                return liveBiomass;
            }

        }

        /// <summary>
        /// Dead Biomass
        /// </summary>
        [JsonIgnore]
        [Units("g/m^2")]
        public PMF.Biomass Dead
        {
            get
            {
                CalculateLiveDead();
                return deadBiomass;
            }

        }

        private void CalculateLiveDead()
        {
            if (Name =="Leaf"  && IsAboveGround is true)
            {
        
                liveBiomass.StructuralWt=GetDM(GrazType.TOTAL, GrazType.ptLEAF)/10;
                liveBiomass.StructuralN=GetDM(GrazType.TOTAL, GrazType.ptLEAF)/10.0 * GetPlantNutr(GrazType.TOTAL, GrazType.ptLEAF, TPlantElement.N);
                deadBiomass.StructuralWt=0;
                deadBiomass.StructuralN=0;

            
            }
            if (Name == "Stem" && IsAboveGround is true)
            {
                liveBiomass.StructuralWt=GetDM(GrazType.TOTAL, GrazType.ptSTEM)/10;
                liveBiomass.StructuralN= GetDM(GrazType.TOTAL, GrazType.ptSTEM)/10.0 * GetPlantNutr(GrazType.TOTAL, GrazType.ptSTEM, TPlantElement.N);
                deadBiomass.StructuralWt=0;
                deadBiomass.StructuralN=0;
            }

        }    


        /// Organ digestibility of live material   
         public double LiveDigestibility
        {
            get
            {   
                if (PastureModel != null)
                {
                    if(Name=="Leaf"  && IsAboveGround is true)
                       return GetDMD(GrazType.TOTAL, GrazType.ptLEAF);
                    if(Name=="Stem"  && IsAboveGround is true)
                       return GetDMD(GrazType.TOTAL, GrazType.ptSTEM);
                    if (Name == "Root" && IsAboveGround is false)
                    {
                        return  GetDMRoot()/10.0 * PastureModel.GetRootConc(GrazType.sgGREEN, GrazType.TOTAL, GrazType.TOTAL, TPlantElement.N);
                    }
                }
                
                return 0;
            }
        }


         /// Organ digestibility of dead material   
         public double DeadDigestibility
        {
            get
            {   
                if (PastureModel != null)
                {
                    if(Name=="Leaf"  && IsAboveGround is true)
                       return 0;
                    if(Name=="Stem"  && IsAboveGround is true)
                       return 0;
                    if (Name == "Root" && IsAboveGround is false)
                    {
                        return  0;
                    }
                }
                
                return 0;
            }
        }
        


       

         /// <summary>
        /// Gets the material components of the organ.
        /// </summary>
        public IEnumerable<DamageableBiomass> Material
        {
            get
            {
                yield return new DamageableBiomass($"{Parent.Name}.{Name}", Live, true, LiveDigestibility);
                yield return new DamageableBiomass($"{Parent.Name}.{Name}", Dead, false, DeadDigestibility);

            }
        } 
         

        // /// <summary>Gets the biomass detached (sent to soil/surface organic matter)</summary>
        // [JsonIgnore]
        // public PMF.Biomass Detached { get; private set; }

        // /// <summary>Gets the biomass removed from the system (harvested, grazed, etc.)</summary>
        // [JsonIgnore]
        // public PMF.Biomass Removed { get; private set; }


        //  /// <summary>Remove biomass from organ.</summary>
        // /// <param name="liveToRemove">Fraction of live biomass to remove from simulation (0-1).</param>
        // /// <param name="deadToRemove">Fraction of dead biomass to remove from simulation (0-1).</param>
        // /// <param name="liveToResidue">Fraction of live biomass to remove and send to residue pool(0-1).</param>
        // /// <param name="deadToResidue">Fraction of dead biomass to remove and send to residue pool(0-1).</param>
        // /// <param name="fractionStanding">Fraction of biomass that remains standing when passed to surfaceOM (0-1).</param>
        // /// <returns>The amount of biomass (live+dead) removed from the plant (g/m2).</returns>
        // public double RemoveBiomass(double liveToRemove, double deadToRemove, double liveToResidue, double deadToResidue, double fractionStanding = 0)
        // {
        //     return RemoveBiomass1(liveToRemove, deadToRemove, liveToResidue, deadToResidue,
        //                                              Live, Dead, Removed, Detached, fractionStanding);
        // }

        // /// <summary>
        // /// Test
        // /// </summary>
        // /// <param name="liveToRemove"></param>
        // /// <param name="deadToRemove"></param>
        // /// <param name="liveToResidue"></param>
        // /// <param name="deadToResidue"></param>
        // /// <param name="live"></param>
        // /// <param name="dead"></param>
        // /// <param name="removed"></param>
        // /// <param name="detached"></param>
        // /// <param name="fractionStanding"></param>
        // /// <param name="writeToSummary"></param>
        // /// <returns></returns>
        // /// <exception cref="Exception"></exception>
        // public double RemoveBiomass1(double liveToRemove, double deadToRemove, double liveToResidue, double deadToResidue,
        //                             PMF.Biomass live, PMF.Biomass dead,
        //                             PMF.Biomass removed, PMF.Biomass detached,
        //                             double fractionStanding = 0,
        //                             bool writeToSummary = true)
        // {
        //     if (liveToRemove + liveToResidue > 1.0)
        //         throw new Exception($"The sum of FractionToResidue and FractionToRemove for {Parent.Name} is greater than one for live biomass.");

        //     if (deadToRemove + deadToResidue > 1.0)
        //         throw new Exception($"The sum of FractionToResidue and FractionToRemove for {Parent.Name} is greater than one for dead biomass");

        //     double liveFractionToRemove = liveToRemove + liveToResidue;
        //     double deadFractionToRemove = deadToRemove + deadToResidue;

        //     if (liveFractionToRemove + deadFractionToRemove > 0.0)
        //     {
        //         double totalBiomass = live.Wt + dead.Wt;
        //         if (totalBiomass > 0)
        //         {
        //             RemoveBiomassFromLiveAndDead(liveToRemove, deadToRemove, liveToResidue, deadToResidue, 
        //                                          live, dead, out PMF.Biomass removing, out PMF.Biomass detaching);


        //             return removing.Wt + detaching.Wt; 


        //         }
        //     }

        //     return 0.0;
        // }

        // /// <summary>Removes biomass from live and dead biomass pools</summary>
        // /// <param name="liveToRemove">Fraction of live biomass to remove from simulation (0-1).</param>
        // /// <param name="deadToRemove">Fraction of dead biomass to remove from simulation (0-1).</param>
        // /// <param name="liveToResidue">Fraction of live biomass to remove and send to residue pool(0-1).</param>
        // /// <param name="deadToResidue">Fraction of dead biomass to remove and send to residue pool(0-1).</param>
        // /// <param name="live">Live biomass pool</param>
        // /// <param name="dead">Dead biomass pool</param>
        // /// <param name="removing">The removed pool to add to.</param>
        // /// <param name="detaching">The amount of detaching material</param>
        // private static void RemoveBiomassFromLiveAndDead(double liveToRemove, double deadToRemove, double liveToResidue, double deadToResidue, 
        //                                                    PMF.Biomass live, PMF.Biomass dead, out PMF.Biomass removing, out PMF.Biomass detaching)
        // {
        //     double remainingLiveFraction = 1.0 - (liveToResidue + liveToRemove);
        //     double remainingDeadFraction = 1.0 - (deadToResidue + deadToRemove);

        //     detaching = live * liveToResidue + dead * deadToResidue;
        //     removing = live * liveToRemove + dead * deadToRemove;

        //     live.Multiply(remainingLiveFraction);
        //     dead.Multiply(remainingDeadFraction);
        // }

        
          


    }
}
