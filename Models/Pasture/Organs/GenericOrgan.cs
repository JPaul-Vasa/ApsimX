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

using Models.PMF.Interfaces;
using Models.PMF;
using Models.PMF.Organs;
using Models.PMF.Library;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Models.GrazPlan.Organs
{

    /// <summary>This is a Organ class with Leaf, Stem and Root. It can be extended to other organs. Currently calculates DM,N and NConc.</summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(Pasture))]
    
    public class GenericOrgan: Model,IStructureDependency,IBiomass,IHasDamageableBiomass,IOrganDamage
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
        /// live biomass of the organ (structural + storage)
        /// </summary>
         public PMF.Biomass Live
        {
            get

            {
                PMF.Biomass mass = new PMF.Biomass();                
                if (PastureModel != null)
                {
                    
                    if (Name=="Leaf"  && IsAboveGround is true)
                    {
                        mass.StructuralWt = GetDM(GrazType.TOTAL, GrazType.ptLEAF)/10.0;  // to g/m2
                        mass.StructuralN = GetDM(GrazType.TOTAL, GrazType.ptLEAF)/10;
                        mass.StorageWt=0;
                        mass.StorageN=0;
                        mass.StorageN=0;
                    
                    }
                    if (Name=="Stem"  && IsAboveGround is true)
                    {
                        mass.StructuralWt = GetDM(GrazType.TOTAL, GrazType.ptSTEM)/10.0;  // to g/m2
                        mass.StructuralN = GetDM(GrazType.TOTAL, GrazType.ptSTEM)/10;
                        mass.StorageWt=0;
                        mass.StorageN=0;
                        mass.StorageN=0;
                    
                    }
                 
                 

                }
                 return mass;
            }
        }

         /// <summary>
        /// Dead biomass of the organ (structural + storage)
        /// </summary>
         public PMF.Biomass Dead
        {
            get
            {   
                PMF.Biomass mass = new PMF.Biomass();
                if (PastureModel != null)
                {
                    if (Name=="Leaf"  && IsAboveGround is true)
                    {
                        mass.StructuralWt = 0;
                        mass.StructuralN = 0;
                        mass.StorageWt=0;
                        mass.StorageN=0;
                        mass.StorageN=0;
                    
                    }
                    if (Name =="Stem"  && IsAboveGround is true)
                    {
                        mass.StructuralWt = 0;
                        mass.StructuralN = 0;
                        mass.StorageWt=0;
                        mass.StorageN=0;
                        mass.StorageN=0;
                    
                    }
                }    


                
                return mass;
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
         

        /// <summary>Gets the biomass detached (sent to soil/surface organic matter)</summary>
        [JsonIgnore]
        public PMF.Biomass Detached { get; private set; }

        /// <summary>Gets the biomass removed from the system (harvested, grazed, etc.)</summary>
        [JsonIgnore]
        public PMF.Biomass Removed { get; private set; }

        private BiomassRemoval biomassRemovalModel = null;

        ///Testing BiomassRemoval
        /// <summary>Remove biomass from organ.</summary>
        /// <param name="liveToRemove">Fraction of live biomass to remove from simulation (0-1).</param>
        /// <param name="deadToRemove">Fraction of dead biomass to remove from simulation (0-1).</param>
        /// <param name="liveToResidue">Fraction of live biomass to remove and send to residue pool(0-1).</param>
        /// <param name="deadToResidue">Fraction of dead biomass to remove and send to residue pool(0-1).</param>
        /// <param name="fractionStanding">Fraction of biomass that remains standing when passed to surfaceOM (0-1).</param>
        /// <returns>The amount of biomass (live+dead) removed from the plant (g/m2).</returns>
        public double RemoveBiomass(double liveToRemove, double deadToRemove, double liveToResidue, double deadToResidue, double fractionStanding = 0)
        {
           return biomassRemovalModel.RemoveBiomass(liveToRemove, deadToRemove, liveToResidue, deadToResidue,
                                                     Live, Dead, Removed, Detached, fractionStanding);
        }
        

        
          


    }
}
