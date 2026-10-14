using UnityEngine;
using System.Collections;

public class RoomsEventsExample : MonoBehaviour
{
    public void StartedRoomTransition(int currentRoom, int previousRoom)
    {
        Debug.Log($"Started Room Transition - Current Room:{currentRoom} - Previous Room:{previousRoom}");
    }

    public void FinishedRoomTransition(int currentRoom, int previousRoom)
    {
        Debug.Log($"Finished Room Transition - Current Room:{currentRoom} - Previous Room:{previousRoom}");
    }
}
