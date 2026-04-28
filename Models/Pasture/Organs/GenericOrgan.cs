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

        //private bool recalculate = false;
        /// <summary>
        /// Live Biomass
        /// </summary>
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
        public PMF.Biomass Dead
        {
            get
            {
                CalculateLiveDead();
                return deadBiomass;
            }

        }




        /// <summary>Calculate the values for calculated states.</summary>
        private void CalculateLiveDead()
        {


           if (PastureModel != null)
            {
                
                if (Name == "Leaf")
                {   
                   //double LeafGreen= GetDM(sgGREEN,GrazType.ptLEAF)/10.0;  // to g/m2
                   double LeafGreen = PastureModel.GetHerbageMass(sgGREEN, ptLEAF, TOTAL); // to g/m2
                   double LeafEst = PastureModel.GetHerbageMass(stESTAB, TOTAL, TOTAL);
                    Console.WriteLine("LeafGreen" + LeafGreen);
                    Console.WriteLine("LeafEst" + LeafEst);
                    liveBiomass.StructuralWt = GetDM(sgGREEN,GrazType.ptLEAF)/10.0;  // to g/m2
                    liveBiomass.StructuralN = GetDM(sgGREEN,GrazType.ptLEAF)/10.0 * GetPlantNutr(sgGREEN,GrazType.ptLEAF, TPlantElement.N); // to g/m2
                
                    deadBiomass.StructuralWt = GetDM(sgDRY,GrazType.ptLEAF)/10.0;  // to g/m2
                    deadBiomass.StructuralN = GetDM(sgDRY,GrazType.ptLEAF)/10.0 * GetPlantNutr(sgGREEN,GrazType.ptLEAF, TPlantElement.N); 
                } 

                if(Name=="Stem")
                {
                    liveBiomass.StructuralWt = GetDM(sgGREEN,GrazType.ptSTEM)/10.0;  // to g/m2
                    liveBiomass.StructuralN = GetDM(sgGREEN,GrazType.ptSTEM)/10.0 * GetPlantNutr(sgGREEN,GrazType.ptSTEM, TPlantElement.N); // to g/m2
                
                    deadBiomass.StructuralWt = GetDM(sgDRY,GrazType.ptSTEM)/10.0;  // to g/m2
                    deadBiomass.StructuralN = GetDM(sgDRY,GrazType.ptSTEM)/10.0 * GetPlantNutr(sgGREEN,GrazType.ptSTEM, TPlantElement.N); 
                }
            }

   
        } 

        /// <summary>
        /// Live digestibility
        /// </summary>
        public double LiveDigestibility
        {
            get
            {   
                 if (PastureModel != null)
                {
                    if (Name == "Leaf")
                    {
                        return GetDMD(sgGREEN, GrazType.ptLEAF);
                    }
                    if (Name == "Stem")
                    {
                        return GetDMD(sgGREEN, GrazType.ptSTEM);
                    }
                    
                }  

                return 0;  
            }
        }

        /// <summary>
        /// Dead digestibility
        /// </summary>
        public double DeadDigestibility
        {
            get
            {   
                 if (PastureModel != null)
                {
                    if (Name == "Leaf")
                    {
                        return GetDMD(sgDRY, GrazType.ptLEAF);
                    }
                    if (Name == "Stem")
                    {
                        return GetDMD(sgDRY, GrazType.ptSTEM);
                    }
                    
                }  

                return 0;  
            }
        }

         /// <summary>A list of material (biomass) that can be damaged.</summary>
         public IEnumerable<DamageableBiomass> Material
        {
            get
            {   
                CalculateLiveDead();
                yield return new DamageableBiomass($"{Parent.Name}.{Name}", Live, true, LiveDigestibility);
                yield return new DamageableBiomass($"{Parent.Name}.{Name}", Dead, false, DeadDigestibility);
            }
        }
private bool needToRecalculateLiveDead = true;
      
        private void RecalculateLiveDead()
{
    if (!needToRecalculateLiveDead)
        return;

    needToRecalculateLiveDead = false;

    liveBiomass.Clear();
    deadBiomass.Clear();

    // Sum over digestibility classes
    int part = (Name == "Leaf") ? GrazType.ptLEAF :
               (Name == "Stem") ? GrazType.ptSTEM : -1;

    if (part < 0)
        return;

    double liveDM = 0, liveN = 0;
    double deadDM = 0, deadN = 0;

    for (int cls = 1; cls <= HerbClassNo; cls++)
    {
        liveDM += PastureModel.GetHerbageMass(stESTAB, part, cls);
        liveN  += PastureModel.GetHerbageNutr(stESTAB, part, cls, TPlantElement.N);

        deadDM += PastureModel.GetHerbageMass(stDEAD, part, cls);
        deadN  += PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.N);
    }

    liveBiomass.StructuralWt = liveDM / 10.0;
    liveBiomass.StructuralN  = liveN  / 10.0;

    deadBiomass.StructuralWt = deadDM / 10.0;
    deadBiomass.StructuralN  = deadN  / 10.0;
}


 /// <summary>
        /// Name of the pasture species for which parameters are to be used
        /// </summary>
        [Description("Species")]
        public string Species { get; set; } = "Perennial Ryegrass";

       /// <summary>
       /// test remove biomass
       /// </summary>
       /// <param name="liveToRemove"></param>
       /// <param name="deadToRemove"></param>
       /// <param name="liveToResidue"></param>
       /// <param name="deadToResidue"></param>
       /// <param name="fractionStanding"></param>
       /// <returns></returns>
        
        public double RemoveBiomass1(
    double liveToRemove = 0,
    double deadToRemove = 0,
    double liveToResidue = 0,
    double deadToResidue = 0,
    double fractionStanding = 0)
{
    double totalDM =
        PastureModel.GetHerbageMass(stESTAB, TOTAL, TOTAL) +
        PastureModel.GetHerbageMass(stDEAD, TOTAL, TOTAL);

    if (totalDM <= 0.0)
        return 0.0;

    BiomassRemoved removed = new BiomassRemoved(2);
    removed.CropType = this.Species;
    removed.DMType[0] = "leaf";
    removed.DMType[1] = "stem";

    double totalRemovedDM = 0.0;

    for (int part = ptLEAF; part <= ptSTEM; part++)
    {
        int idx = part - 1;

        double partLiveRemovedDM = 0.0;
        double partDeadRemovedDM = 0.0;
        double partLiveRemovedN  = 0.0;
        double partDeadRemovedN  = 0.0;
        double partLiveRemovedP  = 0.0;
        double partDeadRemovedP  = 0.0;

        for (int cls = 1; cls <= HerbClassNo; cls++)
        {
            // -----------------------------
            // LIVE POOL
            // -----------------------------
            double liveDM = PastureModel.GetHerbageMass(stESTAB, part, cls);
            double liveN  = PastureModel.GetHerbageNutr(stESTAB, part, cls, TPlantElement.N);
            double liveP  = PastureModel.GetHerbageNutr(stESTAB, part, cls, TPlantElement.P);

            double liveRemoveDM = liveDM * liveToRemove;
            double liveResidueDM = liveDM * liveToResidue;

            double liveRemoveN = liveN * liveToRemove;
            double liveResidueN = liveN * liveToResidue;

            double liveRemoveP = liveP * liveToRemove;
            double liveResidueP = liveP * liveToResidue;

            // Update LIVE pool
            PastureModel.SetHerbageMass(stESTAB, part, cls,
                liveDM - liveRemoveDM - liveResidueDM);

            PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.N,
                liveN - liveRemoveN - liveResidueN);

            PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.P,
                liveP - liveRemoveP - liveResidueP);

            // Accumulate removed (not residue)
            partLiveRemovedDM += liveRemoveDM;
            partLiveRemovedN  += liveRemoveN;
            partLiveRemovedP  += liveRemoveP;

            // -----------------------------
            // DEAD POOL
            // -----------------------------
            double deadDM = PastureModel.GetHerbageMass(stDEAD, part, cls);
            double deadN  = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.N);
            double deadP  = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.P);

            double deadRemoveDM = deadDM * deadToRemove;
            double deadResidueDM = deadDM * deadToResidue;

            double deadRemoveN = deadN * deadToRemove;
            double deadResidueN = deadN * deadToResidue;

            double deadRemoveP = deadP * deadToRemove;
            double deadResidueP = deadP * deadToResidue;

            // Update DEAD pool
            PastureModel.SetHerbageMass(stDEAD, part, cls,
                deadDM - deadRemoveDM - deadResidueDM);

            PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.N,
                deadN - deadRemoveN - deadResidueN);

            PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.P,
                deadP - deadRemoveP - deadResidueP);

            // Accumulate removed (not residue)
            partDeadRemovedDM += deadRemoveDM;
            partDeadRemovedN  += deadRemoveN;
            partDeadRemovedP  += deadRemoveP;
//Console.WriteLine($"Before={liveDM}, After={PastureModel.GetHerbageMass(stESTAB, part, cls)}");
           
            liveBiomass.StructuralWt=PastureModel.GetHerbageMass(stESTAB, part, cls);
            
        }

        // Report removed DM, N, P
        removed.dltCropDM[idx] = partLiveRemovedDM + partDeadRemovedDM;
        removed.dltDM_N[idx]   = partLiveRemovedN  + partDeadRemovedN;
        removed.dltDM_P[idx]   = partLiveRemovedP  + partDeadRemovedP;

        // Fraction to residue (for reporting only)
        removed.FractionToResidue[idx] = liveToResidue + deadToResidue;
         Console.WriteLine(PastureModel.GetHerbageMass(stESTAB, 1, 0));
        totalRemovedDM += removed.dltCropDM[idx];
        
    }
    
    needToRecalculateLiveDead = true;
    return totalRemovedDM;
}

        /// <summary>
        /// test
        /// </summary>
        /// <param name="liveToRemove"></param>
        /// <param name="deadToRemove"></param>
        /// <param name="liveToResidue"></param>
        /// <param name="deadToResidue"></param>
        /// <param name="fractionStanding"></param>
        /// <returns></returns>

        public double RemoveBiomass2(double liveToRemove = 0, double deadToRemove = 0, double liveToResidue = 0, double deadToResidue = 0, double fractionStanding = 0)
        {
            

            double totalDM = PastureModel.GetHerbageMass(stESTAB, TOTAL, TOTAL) +  PastureModel.GetHerbageMass(stDEAD, TOTAL, TOTAL);

             if (totalDM <= 0.0)
                return 0.0;
            // Prepare return object (leaf + stem)
            BiomassRemoved removed = new BiomassRemoved(2);
            removed.CropType = this.Species;
            removed.DMType[0] = "leaf";
            removed.DMType[1] = "stem";
                
            // Loop organs
            for (int part = ptLEAF; part <= ptSTEM; part ++)
                {
                    int idx = part - 1;
                    
                    // Remove biomass proportionally from live + dead
                    double partLiveDM =  PastureModel.GetHerbageMass(stESTAB, part, TOTAL);
                    double  partDeadDM = PastureModel.GetHerbageMass(stDEAD, part, TOTAL);
                    double partLiveRemovedDM = partLiveDM * liveToRemove;
                    double partDeadRemoveDM = partDeadDM * deadToRemove;
                    double partLiveDeadRemoveDM = partLiveRemovedDM + partDeadRemoveDM;

                    removed.dltCropDM[idx]= partLiveDeadRemoveDM;

                    //Remove N proportionally

                    double partLiveN =  PastureModel.GetHerbageNutr(stESTAB, part, TOTAL, TPlantElement.N);
                    double  partDeadN = PastureModel.GetHerbageNutr(stDEAD, part, TOTAL, TPlantElement.N);
                    double partLiveRemoveN = partLiveN * liveToRemove;
                    double partDeadRemoveN = partDeadN * deadToRemove;
                    double partLiveDeadRemoveN = partLiveRemoveN + partDeadRemoveN;
                    removed.dltDM_N[idx] = partLiveDeadRemoveN ;
                    
                     double partLiveP =  PastureModel.GetHerbageNutr(stESTAB, part, TOTAL, TPlantElement.P);
                    double  partDeadP = PastureModel.GetHerbageNutr(stDEAD, part, TOTAL, TPlantElement.N);
                    double partLiveRemoveP = partLiveP * liveToRemove;
                    double partDeadRemoveP = partDeadP * deadToRemove;
                    double partLiveDeadRemoveP = partLiveRemoveP + partDeadRemoveP;
                    removed.dltDM_P[idx] = partLiveDeadRemoveP ;

                     removed.FractionToResidue[idx] = 1.0;

                     for(int cls =1; cls <= HerbClassNo; cls++)
                {
                    double liveDM = PastureModel.GetHerbageMass(stESTAB, part,cls);
                    double RemoveLiveBiomass= liveDM * liveToRemove;
                    PastureModel.SetHerbageMass(stESTAB,part,cls,liveDM-RemoveLiveBiomass);

                    double liveN = PastureModel.GetHerbageNutr(stESTAB,part,cls,TPlantElement.N);
                    double liveP = PastureModel.GetHerbageNutr(stESTAB, part, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.N, liveN - liveN * liveToRemove);
                    PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.P, liveP - liveP * liveToRemove);

                    // Dead
                    double deadDM = PastureModel.GetHerbageMass(stDEAD, part, cls);
                    double deadRemove = deadDM * deadToRemove;
                    PastureModel.SetHerbageMass(stDEAD, part, cls, deadDM - deadToRemove);

                    double deadN = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.N);
                    double deadP = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.N, deadN - deadN * deadToRemove);
                    PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.P, deadP - deadP * deadToRemove);

                     

                }



            }

            Console.WriteLine(PastureModel.GetHerbageMass(stESTAB,1,0));    
            
          return 0.0;  
        }

// /// <summary>
// /// test
// /// </summary>
// /// <param name="liveToRemove"></param>
// /// <param name="deadToRemove"></param>
// /// <param name="liveToResidue"></param>
// /// <param name="deadToResidue"></param>
// /// <param name="fractionStanding"></param>
// /// <returns></returns>
        
//         public double RemoveBiomass(double liveToRemove = 0, double deadToRemove = 0, double liveToResidue = 0, double deadToResidue = 0, double fractionStanding = 0)
//         {
            

//             double totalDM = PastureModel.GetHerbageMass(stESTAB, TOTAL, TOTAL) +  PastureModel.GetHerbageMass(stDEAD, TOTAL, TOTAL);

//              if (totalDM <= 0.0)
//                 return 0.0;
//             // Prepare return object (leaf + stem)
//             BiomassRemoved removed = new BiomassRemoved(2);
//             removed.CropType = this.Species;
//             removed.DMType[0] = "leaf";
//             removed.DMType[1] = "stem";

//             if(Name=="Leaf")
//                 part=1;
//             if(Name=="Stem")
//                 part =2;
//              int idx = part - 1;   
//             // Loop organs
           
//                     double partLiveDM =  PastureModel.GetHerbageMass(stESTAB, part, TOTAL);
//                     double  partDeadDM = PastureModel.GetHerbageMass(stDEAD, part, TOTAL);
//                     double partLiveRemovedDM = partLiveDM * liveToRemove;
//                     double partDeadRemoveDM = partDeadDM * deadToRemove;
//                     double partLiveDeadRemoveDM = partLiveRemovedDM + partDeadRemoveDM;

//                     removed.dltCropDM[idx]= partLiveDeadRemoveDM;

//                      //Remove N proportionally

//                     double partLiveN =  PastureModel.GetHerbageNutr(stESTAB, part, TOTAL, TPlantElement.N);
//                     double  partDeadN = PastureModel.GetHerbageNutr(stDEAD, part, TOTAL, TPlantElement.N);
//                     double partLiveRemoveN = partLiveN * liveToRemove;
//                     double partDeadRemoveN = partDeadN * deadToRemove;
//                     double partLiveDeadRemoveN = partLiveRemoveN + partDeadRemoveN;
//                     removed.dltDM_N[idx] = partLiveDeadRemoveN ;
                    
//                      double partLiveP =  PastureModel.GetHerbageNutr(stESTAB, part, TOTAL, TPlantElement.P);
//                     double  partDeadP = PastureModel.GetHerbageNutr(stDEAD, part, TOTAL, TPlantElement.P);
//                     double partLiveRemoveP = partLiveP * liveToRemove;
//                     double partDeadRemoveP = partDeadP * deadToRemove;
//                     double partLiveDeadRemoveP = partLiveRemoveP + partDeadRemoveP;
//                     removed.dltDM_P[idx] = partLiveDeadRemoveP ;

//                      removed.FractionToResidue[idx] = 1.0;

                
//                     // double liveDM = PastureModel.GetHerbageMass(stESTAB, part,0);
//                     // double RemoveLiveBiomass= liveDM * liveToRemove;
//                     // PastureModel.SetHerbageMass(stESTAB,part,0,liveDM-RemoveLiveBiomass);

//                     // double liveN = PastureModel.GetHerbageNutr(stESTAB,part,0,TPlantElement.N);
//                     // double liveP = PastureModel.GetHerbageNutr(stESTAB, part, 0, TPlantElement.P);
//                     // PastureModel.SetHerbageNutr(stESTAB, part, 0, TPlantElement.N, liveN - liveN * liveToRemove);
//                     // PastureModel.SetHerbageNutr(stESTAB, part, 0, TPlantElement.P, liveP - liveP * liveToRemove);

//                     // // Dead
//                     // double deadDM = PastureModel.GetHerbageMass(stDEAD, part, 0);
//                     // double deadRemove = deadDM * deadToRemove;
//                     // PastureModel.SetHerbageMass(stDEAD, part, 0, deadDM - deadToRemove);

//                     // double deadN = PastureModel.GetHerbageNutr(stDEAD, part, 0, TPlantElement.N);
//                     // double deadP = PastureModel.GetHerbageNutr(stDEAD, part, 0, TPlantElement.P);
//                     // PastureModel.SetHerbageNutr(stDEAD, part, 0, TPlantElement.N, deadN - deadN * deadToRemove);
//                     // PastureModel.SetHerbageNutr(stDEAD, part, 0, TPlantElement.P, deadP - deadP * deadToRemove);

               
//                for (int part = ptLEAF; part <= ptSTEM; part ++)
//                 {      

//                      for(int cls =1; cls <= HerbClassNo; cls++)
//                 {
//                     double liveDM = PastureModel.GetHerbageMass(stESTAB, part,cls);
//                     double RemoveLiveBiomass= liveDM * liveToRemove;
//                     PastureModel.SetHerbageMass(stESTAB,part,cls,liveDM-RemoveLiveBiomass);

//                     double liveN = PastureModel.GetHerbageNutr(stESTAB,part,cls,TPlantElement.N);
//                     double liveP = PastureModel.GetHerbageNutr(stESTAB, part, cls, TPlantElement.P);
//                     PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.N, liveN - liveN * liveToRemove);
//                     PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.P, liveP - liveP * liveToRemove);

//                     // Dead
//                     double deadDM = PastureModel.GetHerbageMass(stDEAD, part, cls);
//                     double deadRemove = deadDM * deadToRemove;
//                     PastureModel.SetHerbageMass(stDEAD, part, cls, deadDM - deadToRemove);

//                     double deadN = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.N);
//                     double deadP = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.P);
//                     PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.N, deadN - deadN * deadToRemove);
//                     PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.P, deadP - deadP * deadToRemove);

//                 }
//                 }
                   

                   
                     


         
            
//           return  partLiveDeadRemoveDM;  
//         }


/// <summary>
/// test
/// </summary>
/// <param name="liveToRemove"></param>
/// <param name="deadToRemove"></param>
/// <param name="liveToResidue"></param>
/// <param name="deadToResidue"></param>
/// <param name="fractionStanding"></param>
/// <returns></returns>
        
        public double RemoveBiomass(double liveToRemove = 0, double deadToRemove = 0, double liveToResidue = 0, double deadToResidue = 0, double fractionStanding = 0)
        {
            

            double totalDM = PastureModel.GetHerbageMass(stESTAB, TOTAL, TOTAL) +  PastureModel.GetHerbageMass(stDEAD, TOTAL, TOTAL);

             if (totalDM <= 0.0)
                return 0.0;

            // // Prepare return object (leaf + stem)
            // BiomassRemoved removed = new BiomassRemoved(2);
            // removed.CropType = this.Species;
            // removed.DMType[0] = "leaf";
            // removed.DMType[1] = "stem";


            if (Name == "Leaf")
            {
                int part=1;
                int idx=part-1;
                 // Remove biomass proportionally from live + dead
                    //double partLiveDM =  PastureModel.GetHerbageMass(stESTAB, part, TOTAL);
                    //double  partDeadDM = PastureModel.GetHerbageMass(stDEAD, part, TOTAL);
                    double partLiveDM =  PastureModel.GetHerbageMass(sgGREEN, part, TOTAL);
                    double  partDeadDM = PastureModel.GetHerbageMass(sgDRY, part, TOTAL);
                    double partLiveRemovedDM = partLiveDM * liveToRemove;
                    double partDeadRemoveDM = partDeadDM * deadToRemove;
                    double partLiveDeadRemoveDM = partLiveRemovedDM + partDeadRemoveDM;

                    //removed.dltCropDM[idx]= partLiveDeadRemoveDM;

                    //Remove N proportionally

                    double partLiveN =  PastureModel.GetHerbageNutr(stESTAB, part, TOTAL, TPlantElement.N);
                    double  partDeadN = PastureModel.GetHerbageNutr(stDEAD, part, TOTAL, TPlantElement.N);
                    double partLiveRemoveN = partLiveN * liveToRemove;
                    double partDeadRemoveN = partDeadN * deadToRemove;
                    double partLiveDeadRemoveN = partLiveRemoveN + partDeadRemoveN;
                   // removed.dltDM_N[idx] = partLiveDeadRemoveN ;
                    
                     double partLiveP =  PastureModel.GetHerbageNutr(stESTAB, part, TOTAL, TPlantElement.P);
                    double  partDeadP = PastureModel.GetHerbageNutr(stDEAD, part, TOTAL, TPlantElement.P);
                    double partLiveRemoveP = partLiveP * liveToRemove;
                    double partDeadRemoveP = partDeadP * deadToRemove;
                    double partLiveDeadRemoveP = partLiveRemoveP + partDeadRemoveP;
                    //removed.dltDM_P[idx] = partLiveDeadRemoveP ;

                   // removed.FractionToResidue[idx] = 1.0;
                    for(int cls =1; cls <= HerbClassNo; cls++)
                {
                    double liveLeafDM = PastureModel.GetHerbageMass(sgGREEN, part, cls);
                    double RemoveLiveBiomass= liveLeafDM * liveToRemove;
                    PastureModel.SetHerbageMass(stESTAB, part, cls, liveLeafDM-RemoveLiveBiomass);

                    double liveStemDM = PastureModel.GetHerbageMass(stESTAB, 2 , cls);
                    PastureModel.SetHerbageMass(stESTAB, 2, cls, liveStemDM);

                    double liveLeafN = PastureModel.GetHerbageNutr(stESTAB, part, cls,TPlantElement.N);
                    double liveLeafP = PastureModel.GetHerbageNutr(stESTAB, part, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.N, liveLeafN  - liveLeafN  * liveToRemove);
                    PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.P, liveLeafP - liveLeafP * liveToRemove);

                    double liveStemN = PastureModel.GetHerbageNutr(stESTAB, 2, cls,TPlantElement.N);
                    double liveStemP = PastureModel.GetHerbageNutr(stESTAB, 2, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stESTAB, 2, cls, TPlantElement.N, liveStemN );
                    PastureModel.SetHerbageNutr(stESTAB, 2, cls, TPlantElement.P, liveStemP);

                    // Dead
                    double deadLeafDM = PastureModel.GetHerbageMass(stDEAD, part, cls);
                    double RemoveDeadLeafDM = deadLeafDM * deadToRemove;
                    PastureModel.SetHerbageMass(stDEAD, part, cls, deadLeafDM -  RemoveDeadLeafDM);

                    double deadStemDM = PastureModel.GetHerbageMass(stDEAD, 2, cls);
                    PastureModel.SetHerbageMass(stDEAD, 2, cls, deadStemDM );




                    double deadLeafN = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.N);
                    double deadLeafP = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.N, deadLeafN - deadLeafN * deadToRemove);
                    PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.P, deadLeafP - deadLeafP * deadToRemove);

                    double deadStemN = PastureModel.GetHerbageNutr(stDEAD, 2, cls, TPlantElement.N);
                    double deadStemP = PastureModel.GetHerbageNutr(stDEAD, 2, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.N, deadStemN);
                    PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.P, deadStemP);
                     

                }

                return partLiveDeadRemoveDM;

            }


            if (Name == "Stem")
            {
                int part = 2;
                int idx = part -1;
                // Remove biomass proportionally from live + dead
                    double partLiveDM =  PastureModel.GetHerbageMass(stESTAB, part, TOTAL);
                    double  partDeadDM = PastureModel.GetHerbageMass(stDEAD, part, TOTAL);
                    double partLiveRemovedDM = partLiveDM * liveToRemove;
                    double partDeadRemoveDM = partDeadDM * deadToRemove;
                    double partLiveDeadRemoveDM = partLiveRemovedDM + partDeadRemoveDM;

                   // removed.dltCropDM[idx]= partLiveDeadRemoveDM;

                    //Remove N proportionally

                    double partLiveN =  PastureModel.GetHerbageNutr(stESTAB, part, TOTAL, TPlantElement.N);
                    double  partDeadN = PastureModel.GetHerbageNutr(stDEAD, part, TOTAL, TPlantElement.N);
                    double partLiveRemoveN = partLiveN * liveToRemove;
                    double partDeadRemoveN = partDeadN * deadToRemove;
                    double partLiveDeadRemoveN = partLiveRemoveN + partDeadRemoveN;
                    //removed.dltDM_N[idx] = partLiveDeadRemoveN ;
                    
                     double partLiveP =  PastureModel.GetHerbageNutr(stESTAB, part, TOTAL, TPlantElement.P);
                    double  partDeadP = PastureModel.GetHerbageNutr(stDEAD, part, TOTAL, TPlantElement.N);
                    double partLiveRemoveP = partLiveP * liveToRemove;
                    double partDeadRemoveP = partDeadP * deadToRemove;
                    double partLiveDeadRemoveP = partLiveRemoveP + partDeadRemoveP;
                    //removed.dltDM_P[idx] = partLiveDeadRemoveP ;

                    // removed.FractionToResidue[idx] = 1.0;

                     for(int cls =0; cls <= 0; cls++)
                {
                   double liveStemDM = PastureModel.GetHerbageMass(stESTAB, part, cls);
                    double RemoveLiveBiomass= liveStemDM  * liveToRemove;
                    PastureModel.SetHerbageMass(stESTAB, part, cls, liveStemDM -RemoveLiveBiomass);

                    double liveLeafDM = PastureModel.GetHerbageMass(stESTAB, 1 , cls);
                    PastureModel.SetHerbageMass(stESTAB, 2, cls, liveLeafDM);

                    double liveStemN = PastureModel.GetHerbageNutr(stESTAB, part, cls,TPlantElement.N);
                    double liveStemP = PastureModel.GetHerbageNutr(stESTAB, part, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.N, liveStemN  - liveStemN  * liveToRemove);
                    PastureModel.SetHerbageNutr(stESTAB, part, cls, TPlantElement.P, liveStemP - liveStemP * liveToRemove);

                    double liveLeafN = PastureModel.GetHerbageNutr(stESTAB, 1, cls,TPlantElement.N);
                    double liveLeafP = PastureModel.GetHerbageNutr(stESTAB, 1, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stESTAB, 1, cls, TPlantElement.N, liveStemN );
                    PastureModel.SetHerbageNutr(stESTAB, 1, cls, TPlantElement.P, liveStemP);

                    // Dead
                    double deadStemDM = PastureModel.GetHerbageMass(stDEAD, part, cls);
                    double RemoveDeadLeafDM = deadStemDM * deadToRemove;
                    PastureModel.SetHerbageMass(stDEAD, part, cls, deadStemDM -  RemoveDeadLeafDM);

                    double deadLeafDM = PastureModel.GetHerbageMass(stDEAD, 1, cls);
                    PastureModel.SetHerbageMass(stDEAD, 1, cls, deadLeafDM );




                    double deadStemN = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.N);
                    double deadStemP = PastureModel.GetHerbageNutr(stDEAD, part, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.N, deadStemN - deadStemN * deadToRemove);
                    PastureModel.SetHerbageNutr(stDEAD, part, cls, TPlantElement.P, deadStemP - deadStemP * deadToRemove);

                    double deadLeafN = PastureModel.GetHerbageNutr(stDEAD, 1, cls, TPlantElement.N);
                    double deadLeafP = PastureModel.GetHerbageNutr(stDEAD, 1, cls, TPlantElement.P);
                    PastureModel.SetHerbageNutr(stDEAD, 1, cls, TPlantElement.N, deadLeafN );
                    PastureModel.SetHerbageNutr(stDEAD, 1, cls, TPlantElement.P, deadLeafP );
                     

                }

                return partLiveDeadRemoveDM;



            }

            return 0.0;
                
           
                
        }














       

       

          


    }
}
