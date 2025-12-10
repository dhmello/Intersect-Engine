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
        /// <summary>
        /// Auto-combat system
        /// Automatically attacks and follows the current target based on user settings
        /// </summary>
        private void UpdateAutoCombat()
        {
            // Check if at least one feature is enabled
            if (!Globals.Database.AutoAttackEnabled && !Globals.Database.AutoFollowEnabled)
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
            
            // If target is within range, try to attack (only if auto-attack is enabled)
            if (distance <= attackRange && Globals.Database.AutoAttackEnabled)
            {
                // Try to attack if not on cooldown
                if (AttackTimer < Timing.Global.Milliseconds)
                {
                    TryAttack();
                }
            }
            else if (distance <= Options.Instance.Combat.MaxPlayerAutoTargetRadius && Globals.Database.AutoFollowEnabled)
            {
                // Target is too far, move towards it (only if auto-follow is enabled)
                if (!IsMoving || MoveTimer < Timing.Global.Milliseconds)
                {
                    MoveTowardsTarget(targetEntity);
                }
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

        /// <summary>
        /// Moves the player towards the target
        /// </summary>
        private void MoveTowardsTarget(IEntity target)
        {
            if (target == null || Globals.Me == null)
            {
                return;
            }

            var myMap = Maps.MapInstance.Get(MapId);
            var targetMap = Maps.MapInstance.Get(target.MapId);

            if (myMap == null || targetMap == null || myMap != targetMap)
            {
                return;
            }

            // Calculate direction to target
            var deltaX = target.X - X;
            var deltaY = target.Y - Y;

            Direction moveDirection = Direction.None;

            // Determine movement direction
            if (Options.Instance.Map.EnableDiagonalMovement)
            {
                // Diagonal movement
                if (deltaY < 0 && deltaX < 0)
                {
                    moveDirection = Direction.UpLeft;
                }
                else if (deltaY < 0 && deltaX > 0)
                {
                    moveDirection = Direction.UpRight;
                }
                else if (deltaY > 0 && deltaX < 0)
                {
                    moveDirection = Direction.DownLeft;
                }
                else if (deltaY > 0 && deltaX > 0)
                {
                    moveDirection = Direction.DownRight;
                }
                else if (deltaY < 0)
                {
                    moveDirection = Direction.Up;
                }
                else if (deltaY > 0)
                {
                    moveDirection = Direction.Down;
                }
                else if (deltaX < 0)
                {
                    moveDirection = Direction.Left;
                }
                else if (deltaX > 0)
                {
                    moveDirection = Direction.Right;
                }
            }
            else
            {
                // Cardinal directions only
                if (Math.Abs(deltaY) > Math.Abs(deltaX))
                {
                    moveDirection = deltaY < 0 ? Direction.Up : Direction.Down;
                }
                else if (deltaX != 0)
                {
                    moveDirection = deltaX < 0 ? Direction.Left : Direction.Right;
                }
            }

            // Set the movement direction and initiate movement
            if (moveDirection != Direction.None)
            {
                DirectionMoving = moveDirection;
                DirectionFacing = moveDirection;
            }
        }
    }
}
