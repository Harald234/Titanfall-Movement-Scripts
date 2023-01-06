public float minX = -60f;
public float maxX = 60f;

public float sensitivity;
public Camera cam;

float rotY = 0f;
float rotX = 0f;

TitanfallMovement moveScript;

void Start()
{
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
    
    moveScript = GetComponent<TitanfallMovement>();
}

void Update()
{
    rotY += Input.GetAxis("Mouse X") * sensitivity;
    rotX += Input.GetAxis("Mouse Y") * sensitivity;

    rotX = Mathf.Clamp(rotX, minX, maxX);

    transform.localEulerAngles = new Vector3(0, rotY, 0);
    cam.transform.localEulerAngles = new Vector3(-rotX, 0, moveScript.tilt);
}
