using UnityEngine;

/// <summary>
/// Attach to medication bottle prefabs that the player retrieves from the dispensary cabinet.
/// Identifies what medication this bottle contains so the hopper can be loaded with it.
/// Tracks whether the bottle has been filled to the prescription target.
/// </summary>
public class MedicationBottle : MonoBehaviour
{
    [SerializeField] private MedicationData medicationData;

    public MedicationData MedicationData => medicationData;

    /// <summary>True once the filling station has dispensed the target pill count into this bottle.</summary>
    public bool IsFilled { get; private set; }

    /// <summary>
    /// Called by PillFillingStation when the target count is reached.
    /// ObjectPickup continues to manage Rigidbody physics — do not touch it here.
    /// </summary>
    public void SetFilled()
    {
        IsFilled = true;
    }
}
