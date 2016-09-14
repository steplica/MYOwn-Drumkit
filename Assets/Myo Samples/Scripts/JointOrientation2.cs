using UnityEngine;
using System.Collections;
using System.Threading;

using LockingPolicy = Thalmic.Myo.LockingPolicy;
using Pose = Thalmic.Myo.Pose;
using UnlockType = Thalmic.Myo.UnlockType;
using VibrationType = Thalmic.Myo.VibrationType;

// Orient the object to match that of the Myo armband.
// Compensate for initial yaw (orientation about the gravity vector) and roll (orientation about
// the wearer's arm) by allowing the user to set a reference orientation.
// Making the fingers spread pose or pressing the 'r' key resets the reference orientation.
public class JointOrientation2 : MonoBehaviour
{
    // Myo game object to connect with.
    // This object must have a ThalmicMyo script attached.
    public GameObject myo = null;

    // A rotation that compensates for the Myo armband's orientation parallel to the ground, i.e. yaw.
    // Once set, the direction the Myo armband is facing becomes "forward" within the program.
    // Set by making the fingers spread pose or pressing "r".
    private Quaternion _antiYaw = Quaternion.identity;

    // A reference angle representing how the armband is rotated about the wearer's arm, i.e. roll.
    // Set by making the fingers spread pose or pressing "r".
    private float _referenceRoll = 0.0f;

    // The pose from the last update. This is used to determine if the pose has changed
    // so that actions are only performed upon making them rather than every frame during
    // which they are active.
    private Pose _lastPose = Pose.Unknown;

    private double x;
    private double y;
    private double padRadius = 25;
    private double drumHeight = 2;

    private bool canDrum = true;
    private bool canCrash = false;
    private bool crashReady = true;
    private float crashDelay = 1.0F;

    // Update is called once per frame.
    void Update()
    {
        // Access the ThalmicMyo component attached to the Myo object.
        ThalmicMyo thalmicMyo = myo.GetComponent<ThalmicMyo>();
        AudioSource[] ass = thalmicMyo.GetComponents<AudioSource>();
        double mx = thalmicMyo.transform.eulerAngles.x;
        double my = thalmicMyo.transform.eulerAngles.y;

        if (mx < 310 && mx > 250)
        {
            canCrash = true;
        }
        if (crashReady && canCrash && mx > 320 && thalmicMyo.accelerometer.magnitude > 1.4)
        {
            ass[4].Play();
            crashReady = false;
            canCrash = false;
            StartCoroutine(crashWait(crashDelay));
        }
        if (canDrum && mx > drumHeight && mx < 270)
        {
            if (padHit(my) != -1)
            {
                ass[padHit(my)].Play();
            }
            canDrum = false;

        }
        else if (mx > 270 || mx < drumHeight)
        {
            canDrum = true;
        }

        // Update references when the pose becomes fingers spread or the q key is pressed.
        bool updateReference = false;
        if (thalmicMyo.pose != _lastPose)
        {
            _lastPose = thalmicMyo.pose;

            if (thalmicMyo.pose == Pose.FingersSpread)
            {
                updateReference = true;

                ExtendUnlockAndNotifyUserAction(thalmicMyo);
            }
        }
        if (Input.GetKeyDown("r"))
        {
            updateReference = true;
        }

        // Update references. This anchors the joint on-screen such that it faces forward away
        // from the viewer when the Myo armband is oriented the way it is when these references are taken.
        if (updateReference)
        {
            // _antiYaw represents a rotation of the Myo armband about the Y axis (up) which aligns the forward
            // vector of the rotation with Z = 1 when the wearer's arm is pointing in the reference direction.
            _antiYaw = Quaternion.FromToRotation(
                new Vector3(myo.transform.forward.x, 0, myo.transform.forward.z),
                new Vector3(0, 0, 1)
            );

            // _referenceRoll represents how many degrees the Myo armband is rotated clockwise
            // about its forward axis (when looking down the wearer's arm towards their hand) from the reference zero
            // roll direction. This direction is calculated and explained below. When this reference is
            // taken, the joint will be rotated about its forward axis such that it faces upwards when
            // the roll value matches the reference.
            Vector3 referenceZeroRoll = computeZeroRollVector(myo.transform.forward);
            _referenceRoll = rollFromZero(referenceZeroRoll, myo.transform.forward, myo.transform.up);

            //print("-------------------------------");
            x = thalmicMyo.transform.eulerAngles.x;
           // print("X: " + x);
            y = thalmicMyo.transform.eulerAngles.y;
          //  print("Y: " + y);
        }

        // Current zero roll vector and roll value.
        Vector3 zeroRoll = computeZeroRollVector(myo.transform.forward);
        float roll = rollFromZero(zeroRoll, myo.transform.forward, myo.transform.up);

        // The relative roll is simply how much the current roll has changed relative to the reference roll.
        // adjustAngle simply keeps the resultant value within -180 to 180 degrees.
        float relativeRoll = normalizeAngle(roll - _referenceRoll);

        // antiRoll represents a rotation about the myo Armband's forward axis adjusting for reference roll.
        Quaternion antiRoll = Quaternion.AngleAxis(relativeRoll, myo.transform.forward);

        // Here the anti-roll and yaw rotations are applied to the myo Armband's forward direction to yield
        // the orientation of the joint.
        transform.rotation = _antiYaw * antiRoll * Quaternion.LookRotation(myo.transform.forward);

        // The above calculations were done assuming the Myo armbands's +x direction, in its own coordinate system,
        // was facing toward the wearer's elbow. If the Myo armband is worn with its +x direction facing the other way,
        // the rotation needs to be updated to compensate.
        if (thalmicMyo.xDirection == Thalmic.Myo.XDirection.TowardWrist)
        {
            // Mirror the rotation around the XZ plane in Unity's coordinate system (XY plane in Myo's coordinate
            // system). This makes the rotation reflect the arm's orientation, rather than that of the Myo armband.
            transform.rotation = new Quaternion(transform.localRotation.x,
                                                -transform.localRotation.y,
                                                transform.localRotation.z,
                                                -transform.localRotation.w);
        }
    }

    // Compute the angle of rotation clockwise about the forward axis relative to the provided zero roll direction.
    // As the armband is rotated about the forward axis this value will change, regardless of which way the
    // forward vector of the Myo is pointing. The returned value will be between -180 and 180 degrees.
    float rollFromZero(Vector3 zeroRoll, Vector3 forward, Vector3 up)
    {
        // The cosine of the angle between the up vector and the zero roll vector. Since both are
        // orthogonal to the forward vector, this tells us how far the Myo has been turned around the
        // forward axis relative to the zero roll vector, but we need to determine separately whether the
        // Myo has been rolled clockwise or counterclockwise.
        float cosine = Vector3.Dot(up, zeroRoll);

        // To determine the sign of the roll, we take the cross product of the up vector and the zero
        // roll vector. This cross product will either be the same or opposite direction as the forward
        // vector depending on whether up is clockwise or counter-clockwise from zero roll.
        // Thus the sign of the dot product of forward and it yields the sign of our roll value.
        Vector3 cp = Vector3.Cross(up, zeroRoll);
        float directionCosine = Vector3.Dot(forward, cp);
        float sign = directionCosine < 0.0f ? 1.0f : -1.0f;

        // Return the angle of roll (in degrees) from the cosine and the sign.
        return sign * Mathf.Rad2Deg * Mathf.Acos(cosine);
    }

    // Compute a vector that points perpendicular to the forward direction,
    // minimizing angular distance from world up (positive Y axis).
    // This represents the direction of no rotation about its forward axis.
    Vector3 computeZeroRollVector(Vector3 forward)
    {
        Vector3 antigravity = Vector3.up;
        Vector3 m = Vector3.Cross(myo.transform.forward, antigravity);
        Vector3 roll = Vector3.Cross(m, myo.transform.forward);

        return roll.normalized;
    }

    // Adjust the provided angle to be within a -180 to 180.
    float normalizeAngle(float angle)
    {
        if (angle > 180.0f)
        {
            return angle - 360.0f;
        }
        if (angle < -180.0f)
        {
            return angle + 360.0f;
        }
        return angle;
    }

    double norm(double angle)
    {
        if (angle > 360)
        {
            return angle - 360;
        }
        if (angle < 0)
        {
            return 360 + angle;
        }
        return angle;
    }

    // pad# = 0, 1, 2
    int padHit(double angle)
    {

        double ll = mod(y - 3 * padRadius, 360);
        double ld = mod(y - padRadius, 360);
        double ud = mod(y + padRadius, 360);
        double ul = mod(y + 3 * padRadius, 360);

        print(angle + ": " + ll + "," + ld + "," + ud + "," + ul);

        // Middle goes negative or positive
        if ((y - padRadius < 0 && (angle > ld || angle <= y)) || (y + padRadius > 360 && (angle >= y || angle < ud)))
        {
          //  print("Middle E");
            return 0;
        }
        // Goes negative to the left
        else if (y - 3 * padRadius < 0 && (angle < ld || angle > ll))
        {
           // print("Left E");
            return 2;
        }
        // Goes > 360 to the right
        else if (y + 3 * padRadius > 360 && (angle > ud || angle < ul))
        {
         //   print("Right E");
            return 1;
        }
        // Perfect case
        else
        {
            // Left drum
            if (angle >= ll && angle < ld)
            {
            //    print("Left");
                return 2;
            }
            // Middle
            else if (angle >= ld && angle <= ud)
            {
             //   print("Middle");
                return 0;
            }
            // Right
            else if (angle > ud && angle <= ul)
            {
             //   print("Right");
                return 1;
            }
        }
        return -1;
    }

    // Extend the unlock if ThalmcHub's locking policy is standard, and notifies the given myo that a user action was
    // recognized.
    void ExtendUnlockAndNotifyUserAction(ThalmicMyo myo)
    {
        ThalmicHub hub = ThalmicHub.instance;

        if (hub.lockingPolicy == LockingPolicy.Standard)
        {
            myo.Unlock(UnlockType.Timed);
        }

        myo.NotifyUserAction();
    }

    double mod(double x, int m)
    {
        return (x % m + m) % m;
    }

    IEnumerator crashWait(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        crashReady = true;
    }
}
