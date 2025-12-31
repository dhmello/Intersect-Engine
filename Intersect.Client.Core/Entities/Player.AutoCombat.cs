using Intersect.Client.Framework.Entities;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.Networking;
using Intersect.Configuration;
using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using System;
using System.Linq;

namespace Intersect.Client.Entities
{
    public partial class Player
    {
        // Flag para prevenir recursão infinita
        [System.Runtime.Serialization.IgnoreDataMember]
        private bool _isProcessingAutoCombat = false;

        /// <summary>
        /// Auto-combat system
        /// Automatically attacks the current target based on user settings
        /// </summary>
        private void UpdateAutoCombat()
        {
            // Prevenir recursão infinita
            if (_isProcessingAutoCombat)
            {
                return;
            }

            try
            {
                _isProcessingAutoCombat = true;

                // Check if auto-attack is enabled
                if (!Globals.Database.AutoAttackEnabled)
                {
                    return;
                }

                // Only process if we have a valid target
                if (TargetId == Guid.Empty || TargetType < 0)
                {
                    return;
                }

                // Get the current target entity
                var targetEntity = GetTargetEntity();
                if (targetEntity == null || targetEntity.IsDisposed)
                {
                    return;
                }

                // IMPORTANT: Check if the target can be attacked
                // This prevents auto-attacking friendly NPCs
                if (!targetEntity.CanBeAttacked)
                {
                    return;
                }

                // Don't process if player is busy
                if (IsBusy || IsAttacking || IsCasting)
                {
                    return;
                }

                // Get the equipped weapon to check for projectile
                var weaponSlot = Options.Instance.Equipment.WeaponSlot;
                ProjectileDescriptor? projectileDescriptor = null;
                var attackRange = 1; // Default melee range

                if (weaponSlot >= 0 && weaponSlot < MyEquipment.Length)
                {
                    var weaponInventorySlot = MyEquipment[weaponSlot];
                    if (weaponInventorySlot >= 0 && weaponInventorySlot < Inventory.Length)
                    {
                        var weaponItem = Inventory[weaponInventorySlot];
                        if (weaponItem != null && ItemDescriptor.TryGet(weaponItem.ItemId, out var itemDescriptor))
                        {
                            // Check if weapon has a projectile
                            if (itemDescriptor.ProjectileId != Guid.Empty)
                            {
                                projectileDescriptor = ProjectileDescriptor.Get(itemDescriptor.ProjectileId);
                                if (projectileDescriptor != null)
                                {
                                    attackRange = projectileDescriptor.Range;
                                }
                            }
                        }
                    }
                }

                // Check if target is in attack range
                var distance = GetDistanceTo(targetEntity);
                
                // If target is within range, try to attack
                if (distance <= attackRange)
                {
                    // Try to attack if not on cooldown
                    if (AttackTimer < Timing.Global.Milliseconds)
                    {
                        TryAttack();
                    }
                }
            }
            catch (Exception)
            {
                // Silently catch and prevent crashes
                // The flag will be reset in finally block
            }
            finally
            {
                _isProcessingAutoCombat = false;
            }
        }

        /// <summary>
        /// Gets the current target entity
        /// </summary>
        private Entity? GetTargetEntity()
        {
            if (TargetId == Guid.Empty)
            {
                return null;
            }

            // Try to get from global entities first
            if (Globals.Entities.TryGetValue(TargetId, out var entity))
            {
                return entity;
            }

            // If not found, search in local map entities
            if (LatestMap is Maps.MapInstance mapInstance)
            {
                foreach (var ent in mapInstance.LocalEntities.Values)
                {
                    if (ent.Id == TargetId)
                    {
                        return ent as Entity;
                    }
                }
            }

            return null;
        }
    }
}
