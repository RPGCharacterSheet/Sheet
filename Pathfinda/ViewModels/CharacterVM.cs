﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoModels.Models;

namespace Pathfinda.ViewModels
{
    public class CharacterVM : Character
    {
        public double GearWeight
        {
            get { return Inventory?.Sum(x => x.WeightCounts ? x.Weight : 0) ?? 0; }
        }

        public Loads EncumbranceByWeight
        {
            get { return Formulas.GetEncumbrance(GearWeight, AbilityScores[Abilities.Strength.ToString()].Score, Size); }
        }

        public Loads EncumbranceByArmor
        {
            get { return (Loads)(Inventory.Where(x => x.Properties.Any(y => y.Key == ItemProperties.ArmorWeight.ToString()))?.Max(x => (int?)x.Properties[ItemProperties.ArmorWeight.ToString()]) ?? 0); }
        }

        public Loads Encumbrance
        {
            get { return (Loads)Math.Max((int)EncumbranceByWeight, (int)EncumbranceByArmor); }
        }

        public int DexModifierForAC
        {
            get { return Math.Min(AbilityScores[Abilities.Dexterity.ToString()].Modifier(), Encumbrance.MaxDexBonus()); }
        }

        private int TryGetItemProperty(InventoryItem item, ItemProperties property, int @default = 0)
        {
            return item.Properties.ContainsKey(property.ToString()) ? item.Properties[property.ToString()] : @default;
        }

        private StatDetails StatsProvidedByInventory(ItemProperties modifier, bool mustBeEquipped = true)
        {
            return StatsProvidedByInventory(new List<ItemProperties>() { modifier }, mustBeEquipped);
        }

        // Aggregate modifiers for relevant items in inventory.
        private StatDetails StatsProvidedByInventory(List<ItemProperties> modifiers, bool mustBeEquipped = true)
        {
            StatDetails details = new StatDetails();
            foreach (var item in Inventory.Where(x => x.Properties.Count > 0 && (!mustBeEquipped || x.IsEquipped)))
            {
                int sumOfRelevantProperties = item.Properties.Sum(x => modifiers.Select(y => y.ToString()).Contains(x.Key) ? x.Value : 0);
                if (sumOfRelevantProperties != 0)
                    details.AddContributingFactor(item.Name, sumOfRelevantProperties);
            }
            return details;
        }

        private StatDetails StatsProvidedByCharacter(ItemProperties modifier)
        {
            return StatsProvidedByCharacter(new List<ItemProperties>() { modifier });
        }

        private StatDetails StatsProvidedByCharacter(List<ItemProperties> modifiers)
        {
            StatDetails details = new StatDetails();
            foreach (var effect in CharacterModifiers.Where(x => modifiers.Contains(x.PropertyModified)))
                details.AddContributingFactor(effect.ModificationReason, effect.Value);
            return details;
        }

        private StatDetails ArmorCalculator(List<ItemProperties> modifiers)
        {
            StatDetails armor = new StatDetails();
            armor.AddContributingFactor("base", 10);
            armor.AddContributingFactor("Dex", DexModifierForAC);
            armor.AddContributingFactor("Size", Size.ACAndAttackBonus());
            armor += StatsProvidedByInventory(modifiers);
            armor += StatsProvidedByCharacter(modifiers);
            return armor;
        }

        public StatDetails ArmorClass
        {
            get
            {
                return ArmorCalculator(new List<ItemProperties>
                {
                    ItemProperties.ArmorBonus,
                    ItemProperties.NaturalArmor,
                    ItemProperties.Dodge,
                    ItemProperties.Deflection
                });
            }
        }

        public StatDetails TouchAC
        {
            get
            {
                return ArmorCalculator(new List<ItemProperties>
                {
                    ItemProperties.Dodge,
                    ItemProperties.Deflection
                });
            }
        }

        public StatDetails FlatFootedAC
        {
            get
            {
                return ArmorCalculator(new List<ItemProperties>
                {
                    ItemProperties.ArmorBonus,
                    ItemProperties.NaturalArmor,
                    ItemProperties.Deflection
                });
            }
        }

        public StatDetails SpellResistance
        {
            get
            {
                return StatsProvidedByInventory(ItemProperties.SpellResistance) + StatsProvidedByCharacter(ItemProperties.SpellResistance);
            }
        }

        public StatDetails FortitudeSave
        {
            get
            {
                StatDetails fortSave = new StatDetails();
                fortSave.AddContributingFactor("Con", AbilityScores[Abilities.Constitution.ToString()].Modifier());
                fortSave += StatsProvidedByInventory(ItemProperties.FortitudeBonus);
                fortSave += StatsProvidedByCharacter(ItemProperties.FortitudeBonus);
                return fortSave;
            }
        }

        public StatDetails ReflexSave
        {
            get
            {
                StatDetails reflexSave = new StatDetails();
                reflexSave.AddContributingFactor("Dex", AbilityScores[Abilities.Dexterity.ToString()].Modifier());
                reflexSave += StatsProvidedByInventory(ItemProperties.ReflexBonus);
                reflexSave += StatsProvidedByCharacter(ItemProperties.ReflexBonus);
                return reflexSave;
            }
        }

        public StatDetails WillSave
        {
            get
            {
                StatDetails willSave = new StatDetails();
                willSave.AddContributingFactor("Wis", AbilityScores[Abilities.Wisdom.ToString()].Modifier());
                willSave += StatsProvidedByInventory(ItemProperties.WillBonus);
                willSave += StatsProvidedByCharacter(ItemProperties.WillBonus);
                return willSave;
            }
        }

        public StatDetails CombatManeuverDefense
        {
            get
            {
                StatDetails cmd = new StatDetails();
                cmd.AddContributingFactor("base", 10);
                cmd.AddContributingFactor("Str", AbilityScores[Abilities.Strength.ToString()].Modifier());
                cmd.AddContributingFactor("Dex", AbilityScores[Abilities.Dexterity.ToString()].Modifier());
                cmd.AddContributingFactor("Size", Size.ACAndAttackBonus());
                cmd += StatsProvidedByInventory(ItemProperties.CombatManeuverDefense);
                cmd += StatsProvidedByCharacter(ItemProperties.CombatManeuverDefense);
                return cmd;
            }
        }

        /// <summary>
        /// BAB is a single number, but it is often represented as +4 or +8/+3 or +14/+10/+6 because you get 3 attacks and use a different BAB per attack
        /// </summary>
        public StatDetails BaseAttackBonus
        {
            get
            {
                StatDetails bab = new StatDetails();
                foreach (Class c in Classes)
                {
                    bab.AddContributingFactor($"{c.Name} level {c.Level}", Math.Floor(c.Level * c.BABGrowth));
                }
                bab += StatsProvidedByInventory(ItemProperties.BaseAttackBonus);
                bab += StatsProvidedByCharacter(ItemProperties.BaseAttackBonus);
                return bab;
            }
        }

        public StatDetails Initiative
        {
            get
            {
                StatDetails init = new StatDetails();
                init.AddContributingFactor("Dex", AbilityScores[Abilities.Dexterity.ToString()].Modifier());
                init += StatsProvidedByInventory(ItemProperties.Initiative);
                init += StatsProvidedByCharacter(ItemProperties.Initiative);
                return init;
            }
        }

        public StatDetails Speed
        {
            get
            {
                StatDetails speed = new StatDetails();
                speed.AddContributingFactor($"{Race.ToString()} race", Race.Speed());
                speed += StatsProvidedByInventory(ItemProperties.Speed);
                speed += StatsProvidedByCharacter(ItemProperties.Speed);

                if (Race != Races.Dwarf && ( Encumbrance == Loads.Medium || Encumbrance == Loads.Heavy))
                {
                    // http://paizo.com/pathfinderRPG/prd/coreRulebook/additionalRules.html#armor-and-encumbrance-for-other-base-speeds
                    var encumberedSpeed = Math.Ceiling(Math.Ceiling(speed.Total * 2 / 3) / 5) * 5;
                    speed.AddContributingFactor(Encumbrance.ToString() + " load", (-1 * speed.Total) + encumberedSpeed); // this is a negative number to take the total down to encumbered speed
                }
                else if (Encumbrance == Loads.Overloaded)
                {
                    speed = new StatDetails();
                    speed.AddContributingFactor("Overloaded!", 5);
                }
                return speed;
            }
        }

        #region Skills
        public StatDetails Acrobatics               { get { return GetSkillBonusFor(ItemProperties.Acrobatics); } }
        public StatDetails Appraise                 { get { return GetSkillBonusFor(ItemProperties.Appraise); } }
        public StatDetails Bluff                    { get { return GetSkillBonusFor(ItemProperties.Bluff); } }
        public StatDetails Climb                    { get { return GetSkillBonusFor(ItemProperties.Climb); } }
        public StatDetails Craft                    { get { return GetSkillBonusFor(ItemProperties.Craft); } }
        public StatDetails Diplomacy                { get { return GetSkillBonusFor(ItemProperties.Diplomacy); } }
        public StatDetails DisableDevice            { get { return GetSkillBonusFor(ItemProperties.DisableDevice); } }
        public StatDetails Disguise                 { get { return GetSkillBonusFor(ItemProperties.Disguise); } }
        public StatDetails EscapeArtist             { get { return GetSkillBonusFor(ItemProperties.EscapeArtist); } }
        public StatDetails Fly                      { get { return GetSkillBonusFor(ItemProperties.Fly); } }
        public StatDetails HandleAnimal             { get { return GetSkillBonusFor(ItemProperties.HandleAnimal); } }
        public StatDetails Heal                     { get { return GetSkillBonusFor(ItemProperties.Heal); } }
        public StatDetails Intimidate               { get { return GetSkillBonusFor(ItemProperties.Intimidate); } }
        public StatDetails KnowledgeArcana          { get { return GetSkillBonusFor(ItemProperties.KnowledgeArcana); } }
        public StatDetails KnowledgeDungeoneering   { get { return GetSkillBonusFor(ItemProperties.KnowledgeDungeoneering); } }
        public StatDetails KnowledgeEngineering     { get { return GetSkillBonusFor(ItemProperties.KnowledgeEngineering); } }
        public StatDetails KnowledgeGeography       { get { return GetSkillBonusFor(ItemProperties.KnowledgeGeography); } }
        public StatDetails KnowledgeHistory         { get { return GetSkillBonusFor(ItemProperties.KnowledgeHistory); } }
        public StatDetails KnowledgeLocal           { get { return GetSkillBonusFor(ItemProperties.KnowledgeLocal); } }
        public StatDetails KnowledgeNature          { get { return GetSkillBonusFor(ItemProperties.KnowledgeNature); } }
        public StatDetails KnowledgeNobility        { get { return GetSkillBonusFor(ItemProperties.KnowledgeNobility); } }
        public StatDetails KnowledgePlanes          { get { return GetSkillBonusFor(ItemProperties.KnowledgePlanes); } }
        public StatDetails KnowledgeReligion        { get { return GetSkillBonusFor(ItemProperties.KnowledgeReligion); } }
        public StatDetails Linguistics              { get { return GetSkillBonusFor(ItemProperties.Linguistics); } }
        public StatDetails Perception               { get { return GetSkillBonusFor(ItemProperties.Perception); } }
        public StatDetails Perform                  { get { return GetSkillBonusFor(ItemProperties.Perform); } }
        public StatDetails Profession               { get { return GetSkillBonusFor(ItemProperties.Profession); } }
        public StatDetails Ride                     { get { return GetSkillBonusFor(ItemProperties.Ride); } }
        public StatDetails SenseMotive              { get { return GetSkillBonusFor(ItemProperties.SenseMotive); } }
        public StatDetails SleightOfHand            { get { return GetSkillBonusFor(ItemProperties.SleightOfHand); } }
        public StatDetails Spellcraft               { get { return GetSkillBonusFor(ItemProperties.Spellcraft); } }
        public StatDetails Stealth                  { get { return GetSkillBonusFor(ItemProperties.Stealth); } }
        public StatDetails Survival                 { get { return GetSkillBonusFor(ItemProperties.Survival); } }
        public StatDetails Swim                     { get { return GetSkillBonusFor(ItemProperties.Swim); } }
        public StatDetails UseMagicDevice           { get { return GetSkillBonusFor(ItemProperties.UseMagicDevice); } }

        public StatDetails GetSkillBonusFor(ItemProperties skill)
        {
            StatDetails details = new StatDetails();
            if (Classes.SelectMany(x => x.ClassSkills).Any(x => x == skill))
                details.AddContributingFactor("Class", 3);
            if (Race.SkillBonuses().Any(x => x == skill))
                details.AddContributingFactor("Race", 2);
            details += StatsProvidedByInventory(skill);
            details += StatsProvidedByCharacter(skill);
            return details;
        }
        #endregion
    }
}
