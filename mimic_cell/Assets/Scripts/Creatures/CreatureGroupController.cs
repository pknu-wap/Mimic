using System.Collections.Generic;
using MimicCell.Player;
using UnityEngine;

namespace MimicCell.Creatures
{
    public enum CreatureGroupState
    {
        Roaming,
        Scattered,
        Regrouping
    }

    [DisallowMultipleComponent]
    public sealed class CreatureGroupController : MonoBehaviour
    {
        [SerializeField]
        private CreatureGroupState currentState =
            CreatureGroupState.Roaming;

        private readonly List<CreatureController> members =
            new List<CreatureController>();

        private CreatureDefinition definition;
        private EcosystemDefinition ecosystem;
        private ICreatureEnvironment environment;
        private PrototypePlayer player;
        private Vector2 roamingDirection;
        private float roamingDirectionTimer;
        private float scatterTimeRemaining;

        public CreatureGroupState CurrentState => currentState;

        public int MemberCount
        {
            get
            {
                int validMemberCount = 0;

                for (int index = 0;
                    index < members.Count;
                    index++)
                {
                    CreatureController member = members[index];

                    if (member != null && member.CurrentState != CreatureState.Dead)
                    {
                        validMemberCount++;
                    }
                }

                return validMemberCount;
            }
        }

        public CreatureDefinition Definition => definition;

        public bool Initialize(
            CreatureDefinition newDefinition,
            EcosystemDefinition newEcosystem,
            ICreatureEnvironment newEnvironment,
            PrototypePlayer targetPlayer)
        {
            if (newDefinition == null ||
                newEcosystem == null ||
                newEnvironment == null ||
                targetPlayer == null)
            {
                Debug.LogError(
                    "CreatureGroupController requires complete initialization data.",
                    this);

                return false;
            }

            if (newDefinition.SpawnKind != CreatureSpawnKind.Group)
            {
                Debug.LogError(
                    "CreatureGroupController requires a group creature definition.",
                    this);

                return false;
            }

            definition = newDefinition;
            ecosystem = newEcosystem;
            environment = newEnvironment;
            player = targetPlayer;
            currentState = CreatureGroupState.Roaming;
            ChooseNewRoamingDirection();

            return true;
        }

        private void ChooseNewRoamingDirection()
        {
            roamingDirection =
                Random.insideUnitCircle.normalized;

            if (roamingDirection.sqrMagnitude <= 0.001f)
            {
                roamingDirection = Vector2.right;
            }

            roamingDirectionTimer = Random.Range(
                ecosystem.GroupSettings.RoamDirectionMinDuration,
                ecosystem.GroupSettings.RoamDirectionMaxDuration);
        }

        private void UpdateRoaming()
        {
            if (currentState != CreatureGroupState.Roaming)
            {
                return;
            }

            roamingDirectionTimer -= Time.deltaTime;

            if (roamingDirectionTimer <= 0f)
            {
                ChooseNewRoamingDirection();
            }

            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController member = members[index];

                if (member == null ||
                    member.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

                Vector2 memberDirection = GetRoamingDirectionForMember(member);

                member.SetGroupMovementDirection(memberDirection);
            }
        }

        private Vector2 CalculateSeparationDirection(CreatureController member)
        {
            Vector2 memberPosition =
                member.transform.position;

            Vector2 separationDirection =
                Vector2.zero;

            float targetSpacing =
                ecosystem.GroupSettings.TargetSpacing;

            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController other =
                    members[index];

                if (other == null || other == member || other.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

                Vector2 awayFromOther =
                    memberPosition -
                    (Vector2)other.transform.position;

                float distance =
                    awayFromOther.magnitude;

                if (distance <= 0.001f || distance >= targetSpacing)
                {
                    continue;
                }

                float separationStrength =
                    1f - distance / targetSpacing;

                separationDirection +=
                    awayFromOther.normalized *
                    separationStrength;
            }

            if (separationDirection.sqrMagnitude <= 0.001f)
            {
                return Vector2.zero;
            }

            return separationDirection.normalized;
        }

        private Vector2 GetRoamingDirectionForMember(CreatureController member)
        {
            Vector2 memberPosition =
                member.transform.position;

            Vector2 groupCenter =
                transform.position;

            float distanceFromCenter = Vector2.Distance(
                memberPosition,
                groupCenter);

            if (distanceFromCenter < ecosystem.GroupSettings.ReturnDistance)
            {
                Vector2 separationDirection =
                    CalculateSeparationDirection(member);

                if (separationDirection.sqrMagnitude <= 0.001f)
                {
                    return roamingDirection;
                }

                Vector2 adjustedDirection =
                    roamingDirection +
                    separationDirection;

                if (adjustedDirection.sqrMagnitude <= 0.001f)
                {
                    return separationDirection;
                }

                return adjustedDirection.normalized;
            }

            Vector2 returnDirection =
                groupCenter - memberPosition;

            if (returnDirection.sqrMagnitude <= 0.001f)
            {
                return roamingDirection;
            }

            return returnDirection.normalized;
        }

        public bool AddMember(CreatureController member)
        {
            if (member == null ||
                members.Contains(member))
            {
                return false;
            }

            if (definition == null ||
                member.Definition != definition)
            {
                Debug.LogWarning(
                    "다른 종류의 생물은 같은 군집에 등록할 수 없습니다.",
                    this);

                return false;
            }

            members.Add(member);
            return true;
        }

        public bool RemoveMember(CreatureController member)
        {
            if (member == null)
            {
                return false;
            }

            bool removed = members.Remove(member);

            if (removed)
            {
                member.ClearGroupMovementDirection();
            }

            return removed;
        }

        public void Disband()
        {
            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController member = members[index];

                if (member != null)
                {
                    member.ClearGroupMovementDirection();
                }
            }

            members.Clear();
            Destroy(gameObject);
        }

        private void Update()
        {
            CleanupMembers();

            if (definition == null)
            {
                return;
            }

            DisbandIfTooSmall();

            if (members.Count < 2)
            {
                return;
            }

            Vector2 groupCenter = CalculateGroupCenter();

            transform.position = new Vector3(
                groupCenter.x,
                groupCenter.y,
                transform.position.z);

            UpdateGroupState();
            UpdateRoaming();
            UpdateScattered();
            UpdateRegrouping();
        }

        private void CleanupMembers()
        {
            for (int index = members.Count - 1;
                index >= 0;
                index--)
            {
                CreatureController member = members[index];

                if (member != null &&
                    member.CurrentState != CreatureState.Dead)
                {
                    continue;
                }

                members.RemoveAt(index);
            }
        }

        private bool IsAnyMemberFleeing()
        {
            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController member = members[index];

                if (member != null &&
                    member.CurrentState == CreatureState.Flee)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateGroupState()
        {
            if (IsAnyMemberFleeing())
            {
                currentState = CreatureGroupState.Scattered;

                scatterTimeRemaining =
                    ecosystem.PerceptionSettings
                        .Tier1FleeDuration;

                return;
            }

            if (currentState != CreatureGroupState.Scattered)
            {
                return;
            }

            scatterTimeRemaining -= Time.deltaTime;

            if (scatterTimeRemaining <= 0f)
            {
                currentState = CreatureGroupState.Regrouping;
            }
        }

        private Vector2 GetDirectionAwayFromPlayer()
        {
            if (player == null)
            {
                return -roamingDirection;
            }

            Vector2 direction =
                (Vector2)transform.position -
                (Vector2)player.transform.position;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return -roamingDirection;
            }

            return direction.normalized;
        }

        private Vector2 GetScatterDirection(int memberIndex)
        {
            Vector2 awayDirection =
                GetDirectionAwayFromPlayer();

            float memberRatio =
                members.Count <= 1
                    ? 0.5f
                    : (float)memberIndex /
                      (members.Count - 1);

            float scatterAngle = Mathf.Lerp(
                -ecosystem.GroupSettings.ScatterAngle,
                ecosystem.GroupSettings.ScatterAngle,
                memberRatio);

            Vector3 rotatedDirection =
                Quaternion.Euler(0f, 0f, scatterAngle) *
                new Vector3(awayDirection.x, awayDirection.y, 0f);

            return new Vector2(rotatedDirection.x, rotatedDirection.y).normalized;
        }

        private void UpdateScattered()
        {
            if (currentState != CreatureGroupState.Scattered)
            {
                return;
            }

            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController member =
                    members[index];

                if (member == null || member.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

                Vector2 scatterDirection =
                    GetScatterDirection(index);

                member.SetGroupMovementDirection(
                    scatterDirection);
            }
        }

        private bool IsRegroupComplete()
        {
            float completionDistance =
                ecosystem.GroupSettings
                    .RegroupCompletionDistance;

            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController member =
                    members[index];

                if (member == null || member.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

                float distanceFromCenter =
                    Vector2.Distance(
                        member.transform.position,
                        transform.position);

                if (distanceFromCenter > completionDistance)
                {
                    return false;
                }
            }

            return members.Count >= 2;
        }

        private void UpdateRegrouping()
        {
            if (currentState != CreatureGroupState.Regrouping)
            {
                return;
            }

            if (IsRegroupComplete())
            {
                currentState =
                    CreatureGroupState.Roaming;

                ChooseNewRoamingDirection();
                return;
            }

            Vector2 groupCenter =
                transform.position;

            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController member =
                    members[index];

                if (member == null || member.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

                Vector2 returnDirection =
                    groupCenter -
                    (Vector2)member.transform.position;

                if (returnDirection.sqrMagnitude <= 0.001f)
                {
                    member.ClearGroupMovementDirection();
                    continue;
                }

                member.SetGroupMovementDirection(
                    returnDirection.normalized);
            }
        }

        private Vector2 CalculateGroupCenter()
        {
            Vector2 positionSum = Vector2.zero;
            int validMemberCount = 0;

            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController member = members[index];

                if (member == null ||
                    member.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

                positionSum += (Vector2)member.transform.position;
                validMemberCount++;
            }

            if (validMemberCount == 0)
            {
                return transform.position;
            }

            return positionSum / validMemberCount;
        }

        private void DisbandIfTooSmall()
        {
            if (MemberCount >= 2)
            {
                return;
            }

            Disband();
        }
    }
}