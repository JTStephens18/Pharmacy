using UnityEngine;

/// <summary>
/// Attach to medication bottle prefabs that the player retrieves from the dispensary cabinet.
/// Identifies what medication this bottle contains so the hopper can be loaded with it.
/// </summary>
public class MedicationBottle : MonoBehaviour
{
    [SerializeField] private MedicationData medicationData;

    public MedicationData MedicationData => medicationData;
}
