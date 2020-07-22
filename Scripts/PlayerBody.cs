using Godot;
using System;
using EventCallback;


/* Idea for the physics of the player
1. We have a variable velocity that is the main velocity where all the other modifiers are added
2. We have sprint, walk, crouch, glide, graple velocities that are added to the main velocity mentioned above
3. Think I'm also going to needa MaxWalkSpeed, MaxSprintSpeed, MaxCrouchSpeed, MaxGrappleSpeed, MaxGlideSpeed 
and depending on the state of the player that maxspeed limmit will be applied.
4. Some movement state for the player is going to be needed to know what max speed ust be applied in movement

*/
public class PlayerBody : KinematicBody
{
    //The point where the grappling hook has hooked
    Vector3 hookPoint = new Vector3();
    //If a hook point is returned
    bool hookPointGet = false;
    //If the grappling hook has been fired
    bool grappling = false;

    CollisionShape bodyCollShape;
    //The raycast that picks up if the player has the ceiling above it
    RayCast ceilingRaycast;
    //The raycast for thte grapple 
    RayCast grappleRay;

    //= New movement variables ====================================================================
    //The direction of travel
    Vector3 direction = new Vector3();
    //The velocity at whitch the player is traveling
    Vector3 velocity = new Vector3();


    //The spatiol object repereseting the gimble of the camera
    Spatial cameraGimbal;
    //The instance of the camera
    Camera camera;

    //The force of the jump
    float jumpSpeed = 5f;
    //How hard the gravity pulls down on the player
    float gravity = -9.8f;
    //The speed at witch the object picks up speed
    float acceleration = 4.5f;
    //The speed at witch the the object losses speed
    float deacceleration = 16f;
    //The maximum slope that can be moved up
    float MAX_SLOPE_ANGLE = 45;
    //How hight the player characters camera displayes when standing
    float defualtHeight = 1.5f;
    //How high the players character camera is when it is crouvhed
    float crouchHeight = .5f;
    //The sensitivity of the mouse, how fast it turns
    float mouseSensitivity = 0.1f;
    // The maximum speeds for all movement ============================================================
    //The maximum walk speed
    float MAX_WALK_SPEED = 7;
    //How sensitive the movement of the mouse is
    float MAX_SPRINT_SPEED = 15;
    //The maximum crouch movement speed the player has
    float MAX_CROUCHING_SPEED = 4;
    //The speed at with the player crouches, goes into a crouching position
    float MAX_CROUCH_SPEED = 3;
    //=============================================================================================
    //If the player is sprinting
    bool isSprinting = false;
    //If the player is crouching
    bool isCrouching = false;
    //Check if the player has pressed the jump button once already
    bool hasJumped = false;
    //If we are gliding it is set to true
    bool isGliding = false;
    //If the players head is touching the cieling, used for crouch stuff
    bool isCollidingWithCeiling = false;
    //=================================================================================================


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {

        Input.SetMouseMode(Input.MouseMode.Captured);

        bodyCollShape = GetNode<CollisionShape>("BodyCollisionShape");

        //The cameras movement gimbal, used for looking mechanics
        grappleRay = GetNode<RayCast>("CameraGimbal/Camera/GrappleRay");
        //The raycast on the player body that detects cieling collisions
        ceilingRaycast = GetNode<RayCast>("CeilingCollisionRay");
        //Grab the refference to the cameras gimbals
        cameraGimbal = GetNode<Spatial>("CameraGimbal");
        //Grab the refference to the camera
        camera = GetNode<Camera>("CameraGimbal/Camera");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(float delta)
    {
        //Check if the head ray collider is active
        if (ceilingRaycast.IsColliding()) isCollidingWithCeiling = true;

        //Grabs the input 
        ProcessInput(delta);
        //Processes the input and moves the charecter
        PrecessMovement(delta);
        /*
                Grapple();
                //If the player hits a ceiling he does not "stick" to it for a second then fall, it makes you fall emediatly
                if (headBumped) fall.y = -2;
                */
    }

    private void ProcessInput(float delta)
    {
        //The initial move direction for the player
        direction = new Vector3();
        //The reference to the cameras global transformw
        Transform camTransform = camera.GlobalTransform;
        //Get the movement vector here 
        Vector2 inputMovementVector = new Vector2();
        //Check what movement buttons are pressed
        if (Input.IsActionPressed("MoveForward"))
            inputMovementVector.y += 1;
        else if (Input.IsActionPressed("MoveBackward"))
            inputMovementVector.y -= 1;
        if (Input.IsActionPressed("StrafeLeft"))
            inputMovementVector.x -= 1;
        else if (Input.IsActionPressed("StrafeRight"))
            inputMovementVector.x += 1;
        //Normalize the input vector to not get faster movement when moving diagonally
        inputMovementVector = inputMovementVector.Normalized();
        //set the direction using the cameras transform basis multiplied with the inout values
        direction += -camTransform.basis.z * inputMovementVector.y;
        direction += camTransform.basis.x * inputMovementVector.x;
        //If the player is on the floor then set the hasjumped and isGliding checks to false
        //Can't think of a better way to do this now than brute forcing it just before the Jump button check
        if (IsOnFloor())
        {
            hasJumped = false;
            isGliding = false;
        }
        //If we press jump this method is called once
        if (Input.IsActionJustPressed("Jump"))
        {
            //If the player is on the floor
            if (IsOnFloor())
            { //We set the velocity to the jump velocity
                velocity.y = jumpSpeed;
                //We set the hasJumped to true
                hasJumped = true;
                GD.Print("Jumped");
            }
            else
            {
                //If the player is not on thet floow and is gliding and 
                if (hasJumped)
                {
                    //Togle gliding on or of
                    isGliding = !isGliding;
                    GD.Print("isGliding = " + isGliding);
                }
            }
        }
        //  Capturing/Freeing the cursor
        if (Input.IsActionJustPressed("ui_cancel"))
        {
            if (Input.GetMouseMode() == Input.MouseMode.Visible)
                Input.SetMouseMode(Input.MouseMode.Captured);
            else
                Input.SetMouseMode(Input.MouseMode.Visible);
        }
        //If the sprint button is pressed we set sprinting to true
        if (Input.IsActionPressed("Sprint")) isSprinting = true;
        else isSprinting = false;
        //If the player is crouching and the collision shape is a capsule then 
        if (Input.IsActionPressed("Crouch") && bodyCollShape.Shape is CapsuleShape capShape)
        {
            capShape.Height -= MAX_CROUCH_SPEED * delta;
            isCrouching = true;
        }
        else if (!isCollidingWithCeiling)
        {
            ((CapsuleShape)bodyCollShape.Shape).Height += MAX_CROUCH_SPEED * delta;
            isCrouching = false;
        }
        //Clamp the max and min height for crouching when it is being modified
        ((CapsuleShape)bodyCollShape.Shape).Height = Mathf.Clamp(((CapsuleShape)bodyCollShape.Shape).Height, crouchHeight, defualtHeight);
    }

    private void PrecessMovement(float delta)
    {

        //Set the directions y to zero as the jump physics will be added later
        direction.y = 0;
        //Normalize the direction
        direction = direction.Normalized();
        //The lift calculated that will be applied to the gravity calculations
        float lift = 0;
        //If we were gliding but are now on the ground we disable the isGliding
        /*
        if (isGliding)
        {
            //We grab the current velocity 
            //lift  = velocity * delta;
            //Don't know if this wil work or not
            lift = Mathf.Abs((velocity.x + velocity.z));

        }
        */
        //We add the gravity to the velocities y axis
        velocity.y += delta * (gravity + lift);
        //We clamp the gravity so the player doesn't start falling into the sky 
        Mathf.Clamp(velocity.y, gravity, -1);

        //we set the velocity to a temporary velocity to add some pysics work
        Vector3 hvel = velocity;
        //We make sure that the tem horizontal velocities y axis for jumping is set to zero; 
        hvel.y = 0;
        //we create another temporary vector3 to us for the movement speed calculations
        Vector3 target = direction;
        //We set the maximum movement speed her, later more max move speeds will be added for crouching, sprinting and gliding
        if (isSprinting) target *= MAX_SPRINT_SPEED;
        else if (isCrouching) target *= MAX_CROUCH_SPEED;
        else target *= MAX_WALK_SPEED;
        //Create the aceleration variable to be used
        float accel;
        //Check if the dot product for the direction vec3 is greater than zero, if it is we set the accel to acceleration else we set it to deaccelerate
        if (direction.Dot(hvel) > 0) accel = acceleration;
        else accel = deacceleration;
        //We then linear interpolate the horizontal velocity with the target by the accel amount
        hvel = hvel.LinearInterpolate(target, accel * delta);
        //We set the velocity to the newly interpolated hvel vec3
        velocity.x = hvel.x;
        velocity.z = hvel.z;
        //We then call the move and slide method with the new velocity values
        velocity = MoveAndSlide(velocity, Vector3.Up, true, 4, Mathf.Deg2Rad(MAX_SLOPE_ANGLE));

    }
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion eventMouseMotion && Input.GetMouseMode() == Input.MouseMode.Captured)
        {
            //RotateY(Mathf.Deg2Rad(-@event.relative.x * mouseSensitivity)) 
            cameraGimbal.RotateX(Mathf.Deg2Rad(-eventMouseMotion.Relative.y * mouseSensitivity));
            RotateY(Mathf.Deg2Rad(-eventMouseMotion.Relative.x * mouseSensitivity));

            //cameraGimbal.Rotation = new Vector3(Mathf.Clamp(cameraGimbal.Rotation.x, Mathf.Deg2Rad(-90), Mathf.Deg2Rad(90)), cameraGimbal.Rotation.y, cameraGimbal.Rotation.z);
            Vector3 camRotation = cameraGimbal.RotationDegrees;
            camRotation.x = Mathf.Clamp(camRotation.x, -70f, 70f);
            cameraGimbal.RotationDegrees = camRotation;
        }
    }



    /*
        public void Grapple()
        {
            if (Input.IsActionJustPressed("Ability"))
            {
                if (grappleRay.IsColliding())
                {
                    if (!grappling)
                    {
                        grappling = true;
                    }
                }
            }
            if (grappling)
            {
                fall.y = 0;
                if (!hookPointGet)
                {
                    hookPoint = grappleRay.GetCollisionPoint() + new Vector3(0, 1.25f, 0);
                    hookPointGet = true;
                }
                if (hookPoint.DistanceTo(Transform.origin) > 1f)
                {
                    if (hookPointGet)
                    {
                        Transform = new Transform(Transform.basis, Transform.origin.LinearInterpolate(hookPoint, 0.05f));
                    }
                    else
                    {
                        grappling = false;
                        hookPointGet = false;
                    }
                }
            }
            if (ceilingRaycast.IsColliding())
            {
                grappling = false;
                hookPoint = new Vector3();
                hookPointGet = false;
                GlobalTranslate(new Vector3(0, -1, 0));
            }
        }
        */
}
