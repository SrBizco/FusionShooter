using Fusion;
using UnityEngine;

public class PlayerVisualSelector : NetworkBehaviour
{
    private enum VisualGroup
    {
        None = -1,
        FreeForAll = 0,
        Blue = 1,
        Red = 2
    }

    [Header("Model Lists")]
    [SerializeField] private GameObject[] freeForAllModels;
    [SerializeField] private GameObject[] blueTeamModels;
    [SerializeField] private GameObject[] redTeamModels;

    [Header("References")]
    [SerializeField] private Transform visualParent;
    [SerializeField] private Transform defaultVisualRoot;
    [SerializeField] private Transform weaponRoot;
    [SerializeField] private RuntimeAnimatorController animatorController;

    [Header("Weapon Hand Offset")]
    [SerializeField] private Vector3 weaponLocalPosition = new(0.069f, -0.001f, 0.005f);
    [SerializeField] private Vector3 weaponLocalEulerAngles = new(190f, 250f, 15f);
    [SerializeField] private Vector3 weaponLocalScale = Vector3.one;

    [Networked] private NetworkBool VisualSelected { get; set; }
    [Networked] private int SelectedGroup { get; set; }
    [Networked] private int SelectedIndex { get; set; }

    private PlayerStats playerStats;
    private PlayerAnimationController animationController;
    private Animator initialAnimator;
    private GameObject spawnedVisual;
    private int appliedGroup = int.MinValue;
    private int appliedIndex = int.MinValue;

    public override void Spawned()
    {
        ResolveReferences();

        if (Object.HasStateAuthority)
            TrySelectVisual();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && !VisualSelected)
            TrySelectVisual();
    }

    public override void Render()
    {
        if (!VisualSelected)
            return;

        ApplySelectedVisual();
    }

    private void ResolveReferences()
    {
        if (visualParent == null)
            visualParent = transform;

        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (animationController == null)
            animationController = GetComponent<PlayerAnimationController>();

        if (initialAnimator == null)
            initialAnimator = GetComponentInChildren<Animator>(true);

        if (defaultVisualRoot == null && initialAnimator != null)
            defaultVisualRoot = initialAnimator.transform;

        if (weaponRoot == null)
            weaponRoot = FindChildRecursive(transform, "Weapon");

        if (animatorController == null && animationController != null)
            animatorController = animationController.CurrentController;

        if (animatorController == null && initialAnimator != null)
            animatorController = initialAnimator.runtimeAnimatorController;
    }

    private void TrySelectVisual()
    {
        ResolveReferences();

        VisualGroup group = GetGroupForCurrentPlayer();
        GameObject[] models = GetModels(group);
        if (models == null || models.Length == 0)
            return;

        SelectedGroup = (int)group;
        SelectedIndex = Random.Range(0, models.Length);
        VisualSelected = true;
    }

    private VisualGroup GetGroupForCurrentPlayer()
    {
        if (MatchSettings.CurrentMode != MatchMode.TeamDeathmatch)
            return VisualGroup.FreeForAll;

        if (playerStats == null || playerStats.Team == PlayerTeam.None)
            return VisualGroup.None;

        return playerStats.Team == PlayerTeam.Blue ? VisualGroup.Blue : VisualGroup.Red;
    }

    private GameObject[] GetModels(VisualGroup group)
    {
        return group switch
        {
            VisualGroup.Blue => blueTeamModels,
            VisualGroup.Red => redTeamModels,
            VisualGroup.FreeForAll => freeForAllModels != null && freeForAllModels.Length > 0
                ? freeForAllModels
                : GetFirstAvailableTeamModels(),
            _ => null
        };
    }

    private GameObject[] GetFirstAvailableTeamModels()
    {
        if (blueTeamModels != null && blueTeamModels.Length > 0)
            return blueTeamModels;

        return redTeamModels;
    }

    private void ApplySelectedVisual()
    {
        if (appliedGroup == SelectedGroup && appliedIndex == SelectedIndex)
            return;

        ResolveReferences();

        VisualGroup group = (VisualGroup)SelectedGroup;
        GameObject[] models = GetModels(group);
        if (models == null || SelectedIndex < 0 || SelectedIndex >= models.Length || models[SelectedIndex] == null)
            return;

        if (spawnedVisual != null)
            Destroy(spawnedVisual);

        spawnedVisual = Instantiate(models[SelectedIndex], visualParent);
        spawnedVisual.transform.localPosition = Vector3.zero;
        spawnedVisual.transform.localRotation = Quaternion.identity;
        spawnedVisual.transform.localScale = Vector3.one;

        DisableGameplayColliders(spawnedVisual);

        Animator spawnedAnimator = spawnedVisual.GetComponentInChildren<Animator>(true);
        if (spawnedAnimator != null)
        {
            if (animatorController != null)
                spawnedAnimator.runtimeAnimatorController = animatorController;

            animationController?.SetAnimator(spawnedAnimator, animatorController);
            AttachWeaponToHand(spawnedAnimator);
        }
        else
        {
            Debug.LogWarning($"Visual prefab {models[SelectedIndex].name} has no Animator.");
        }

        if (defaultVisualRoot != null && defaultVisualRoot.gameObject != spawnedVisual)
            defaultVisualRoot.gameObject.SetActive(false);

        appliedGroup = SelectedGroup;
        appliedIndex = SelectedIndex;
    }

    private void AttachWeaponToHand(Animator animator)
    {
        if (weaponRoot == null || animator == null)
            return;

        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
            hand = FindChildRecursive(animator.transform, "Hand_R");

        if (hand == null)
        {
            Debug.LogWarning($"Could not find right hand for visual {animator.name}.");
            return;
        }

        weaponRoot.SetParent(hand, false);
        weaponRoot.localPosition = weaponLocalPosition;
        weaponRoot.localRotation = Quaternion.Euler(weaponLocalEulerAngles);
        weaponRoot.localScale = weaponLocalScale;
        weaponRoot.gameObject.SetActive(true);
    }

    private void DisableGameplayColliders(GameObject visual)
    {
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }
}
