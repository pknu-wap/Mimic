using System.Collections.Generic;
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
        private Vector2 roamingDirection;
        private float roamingDirectionTimer;
        private Vector2 lastThreatPosition;
        private float lastThreatDetectedAt = float.NegativeInfinity;

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
            ICreatureEnvironment newEnvironment)
        {
            if (newDefinition == null ||
                newEcosystem == null ||
                newEnvironment == null)
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
            currentState = CreatureGroupState.Roaming;
            lastThreatDetectedAt = float.NegativeInfinity;
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

            float edgeSpacing =
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

                Vector2 separationAxis =
                    distance > 0.001f
                        ? awayFromOther / distance
                        : GetOverlapSeparationAxis(
                            member,
                            other);

                float requiredCenterDistance =
                    member.GetRequiredCenterDistance(
                        other,
                        separationAxis,
                        edgeSpacing);

                if (requiredCenterDistance <= 0.001f ||
                    distance >= requiredCenterDistance)
                {
                    continue;
                }

                float separationStrength =
                    1f -
                    distance / requiredCenterDistance;

                separationDirection +=
                    separationAxis *
                    separationStrength;
            }

            if (separationDirection.sqrMagnitude <= 0.001f)
            {
                return Vector2.zero;
            }

            return separationDirection.normalized;
        }

        private static Vector2 GetOverlapSeparationAxis(
            CreatureController member,
            CreatureController other)
        {
            return member.GetInstanceID() < other.GetInstanceID()
                ? Vector2.left
                : Vector2.right;
        }

        private float CalculateRepresentativeCenterSpacing()
        {
            float diameterSum = 0f;
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

                diameterSum +=
                    member.GetMaximumColliderDiameter();

                validMemberCount++;
            }

            float averageDiameter =
                validMemberCount > 0
                    ? diameterSum / validMemberCount
                    : 0f;

            return averageDiameter +
                ecosystem.GroupSettings.TargetSpacing;
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

            float returnDistance =
                ecosystem.GroupSettings.GetReturnDistance(
                    CalculateRepresentativeCenterSpacing());

            if (distanceFromCenter < returnDistance)
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
            ClearMemberMovement();
            members.Clear();
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            ClearMemberMovement();
        }

        private void ClearMemberMovement()
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
        }

        private void LateUpdate()
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

        private bool TryGetClosestReportedThreat(
            out Vector2 threatPosition,
            out float latestDetectionTime)
        {
            threatPosition = Vector2.zero;
            latestDetectionTime = float.NegativeInfinity;
            float closestDistanceSquared = float.PositiveInfinity;
            bool found = false;

            for (int index = 0;
                index < members.Count;
                index++)
            {
                CreatureController member = members[index];

                if (member == null ||
                    !member.TryGetDetectedThreat(
                        out FoodChainTarget threat,
                        out Vector2 detectedPosition,
                        out float detectedAt) ||
                    !threat.IsValid ||
                    Time.time - detectedAt >=
                        ecosystem.PerceptionSettings.Tier1FleeDuration)
                {
                    continue;
                }

                latestDetectionTime = Mathf.Max(
                    latestDetectionTime,
                    detectedAt);

                float distanceSquared =
                    (detectedPosition -
                        (Vector2)transform.position).sqrMagnitude;

                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    threatPosition = detectedPosition;
                    found = true;
                }
            }

            return found;
        }

        private void UpdateGroupState()
        {
            if (TryGetClosestReportedThreat(
                    out Vector2 threatPosition,
                    out float latestDetectionTime))
            {
                lastThreatPosition = threatPosition;
                lastThreatDetectedAt = Mathf.Max(
                    lastThreatDetectedAt,
                    latestDetectionTime);

                currentState = CreatureGroupState.Scattered;
            }

            if (currentState != CreatureGroupState.Scattered)
            {
                return;
            }

            if (Time.time - lastThreatDetectedAt >=
                ecosystem.PerceptionSettings.Tier1FleeDuration)
            {
                currentState = CreatureGroupState.Regrouping;

                for (int index = 0;
                    index < members.Count;
                    index++)
                {
                    if (members[index] != null)
                    {
                        members[index].EndGroupFlee();
                    }
                }
            }
        }

        private Vector2 GetDirectionAwayFromThreat()
        {
            Vector2 direction =
                (Vector2)transform.position -
                lastThreatPosition;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return -roamingDirection;
            }

            return direction.normalized;
        }

        private Vector2 GetScatterDirection(int memberIndex)
        {
            Vector2 awayDirection =
                GetDirectionAwayFromThreat();

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
                    scatterDirection,
                    fleeing: true);
            }
        }

        private bool IsRegroupComplete()
        {
            float completionDistance =
                ecosystem.GroupSettings
                    .GetRegroupCompletionDistance(
                        CalculateRepresentativeCenterSpacing());

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
                UpdateRoaming();
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
