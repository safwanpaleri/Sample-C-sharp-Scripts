using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using LMNT;

public class NPCController : MonoBehaviour
{
    //Paste your api key and workspace Id in here from InWorldAI account
    [Header("InWorldAI keys & Id")]
    private string apiKey = "";
    string workspaceId = "";

    //variable to save previous conversation
    string last_cube_convo = "";
    string last_sphere_convo = "";
    string last_player_convo = "";

    //LMNTSpeech is a free text to speech asset, add "LMNTSpeech" script to the gameobject and attach to this script
    [SerializeField] private LMNTSpeech CubeSpeech;
    [SerializeField] private LMNTSpeech SphereSpeech;
    [SerializeField] private LMNTSpeech PlayerSpeech;

    //if the player inside the range of this radius can enter the speech with the gameobject which is in range.
    [SerializeField] private float ActiveRadius = 7f;

    //Showing subtitle of the generated script
    [SerializeField] private Text ConvoSubtitle;

    //Total no of conversation required, if not specified it will not create conversation,
    [SerializeField] private int TotalConversationExchange = 14;

    //other required variables
    private bool isPlayerActive = false;
    private bool firstTime = true;
    private bool convoCompleted = false;
    private int number_of_exchanges = 0;

    //classes created for api calls, it contains classes for request and result class
    #region API Serilizable Classes

    [System.Serializable]
    private class RequestData
    {
        public string character;
        public string text;
        public string endUserFullname;
        public string endUserId;
    }

    [System.Serializable]
    private class EmotionData
    {
        public string behavior;
        public string strength;
    }

    [System.Serializable]
    private class ResponseData
    {
        public string name;
        public string[] textList;
        public EmotionData emotion;
    }

    #endregion

    void Start()
    {
        //Initializing the first conversation.
        StartCoroutine(SphereConvo("How's the plan going", CubeSpeech.gameObject.name));
    }
    

    // Update is called once per frame
    void Update()
    {
        //Checking whether the player inside the rane to have a conversation.
        if(Vector3.Distance(PlayerSpeech.gameObject.transform.position, CubeSpeech.transform.position) <= ActiveRadius ||
            Vector3.Distance(PlayerSpeech.gameObject.transform.position, SphereSpeech.transform.position) <= ActiveRadius)
        {
            isPlayerActive = true;
        }
        else
            isPlayerActive = false;

    }

    //Cube is the name of character in my game, so created a function which controls the conversation of that object/character.
    IEnumerator CubeConvo(string last_convo, string askedBy)
    {
        //setting up URL, and requestdata
        //request data contains character which is created in InWorldAi workspace, the last message, who spoke to this character, and userid if applicable.
        //the class is converted in json, from then converted in byte string and then assigned as new uploadhandlerRaw. and send the request.
        //if the request is successfull then result is converted into json using the class we created earlier,
        //if the number of exchanges happened, then end the conversation,
        //identify the next speaker and prefetch the convo
        //then convert the convo into speech and after other completed their speech and after a random pause starts talking
        //if the player joined for the first time give priority to player, else randomly select the character to for the next conversation
        //and then increment the number of conversation happened
        string apiURL = $"https://studio.inworld.ai/v1/workspaces/{workspaceId}/characters/{Character_name_from_inworldai}:simpleSendText";
        RequestData requestData = new RequestData
        {
            character = $"workspaces/{workspaceId}/characters/{Character_name_from_inworldai}",
            text = last_convo,
            endUserFullname = askedBy,
            endUserId = "12345"
        };

        string requestDataJson = JsonUtility.ToJson(requestData);

        UnityWebRequest request = new UnityWebRequest(apiURL, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestDataJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("authorization", "Basic " + apiKey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("POST request error: " + request.error);
        }
        else
        {
            ResponseData responseData = JsonUtility.FromJson<ResponseData>(request.downloadHandler.text);

            // Access the message text
            if (responseData.textList != null && responseData.textList.Length > 0)
            {
                string messageText = "";
                foreach(string message in responseData.textList)
                {
                    messageText += " " + message;
                }

                
                if (number_of_exchanges > TotalConversationExchange)
                {
                    messageText = "Well. its decided then, see you guys later!";
                    convoCompleted = true;
                }
                last_cube_convo = messageText;
                CubeSpeech.dialogue = messageText;
                SphereSpeech._handler = null;
                PlayerSpeech._handler = null;
                StartCoroutine(CubeSpeech.Prefetch());

                yield return new WaitUntil(() => !CubeSpeech.isFetching && !SphereSpeech._audioSource.isPlaying && !PlayerSpeech._audioSource.isPlaying);
                yield return new WaitForSeconds(Random.Range(1.0f, 2.0f));
                StartCoroutine(CubeSpeech.Talk());
                ConvoSubtitle.text = CubeSpeech.gameObject.name + ": " + messageText;
                if(!convoCompleted)
                {
                    if (isPlayerActive)
                    {
                        if (firstTime)
                        {
                            StartCoroutine(PlayerConvo("Ah!, nice to meet you zack, we were talking about " + last_cube_convo, CubeSpeech.gameObject.name));
                            firstTime = false;
                        }
                        else
                        {
                            int rand = Random.Range(1, 3);
                            if (rand == 1)
                            {
                                StartCoroutine(SphereConvo(last_cube_convo, CubeSpeech.gameObject.name));
                            }
                            else
                            {
                                StartCoroutine(PlayerConvo(last_cube_convo, CubeSpeech.gameObject.name));
                            }
                        }
                    }
                    else
                    {
                        StartCoroutine(SphereConvo(last_cube_convo, CubeSpeech.gameObject.name));
                    }
                }
            }
            if (responseData.emotion != null)
            {
                string behavior = responseData.emotion.behavior;
                string strength = responseData.emotion.strength;
            }
            number_of_exchanges++;
        }
    }

    //Sphere is the name of character in my game, so created a function which controls the conversation of that object/character.
    IEnumerator SphereConvo(string last_convo, string askedBy)
    {
        //exactly same to the above functionality, but just changes the character.
        string apiURL = $"https://studio.inworld.ai/v1/workspaces/{workspaceId}/characters/{Character_name_from_InWorldAi}:simpleSendText";
        RequestData requestData = new RequestData
        {
            character = $"workspaces/{workspaceId}/characters/{Character_name_from_InWorldAi}",
            text = last_convo,
            endUserFullname = askedBy,
            endUserId = "12345"
        };

        string requestDataJson = JsonUtility.ToJson(requestData);

        UnityWebRequest request = new UnityWebRequest(apiURL, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestDataJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("authorization", "Basic " + apiKey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("POST request error: " + request.error);
        }
        else
        {
            ResponseData responseData = JsonUtility.FromJson<ResponseData>(request.downloadHandler.text);

            // Access the message text
            if (responseData.textList != null && responseData.textList.Length > 0)
            {
                string messageText = "";
                foreach (string message in responseData.textList)
                {
                    messageText += " " + message;
                }

                if (number_of_exchanges > TotalConversationExchange)
                {
                    messageText = "Well. its decided then, see you guys later!";
                    convoCompleted = true;
                }
                last_sphere_convo = messageText;
                SphereSpeech.dialogue = messageText;
                CubeSpeech._handler = null;
                PlayerSpeech._handler = null;
                StartCoroutine(SphereSpeech.Prefetch());
                yield return new WaitUntil(() => !SphereSpeech.isFetching && !CubeSpeech._audioSource.isPlaying && !PlayerSpeech._audioSource.isPlaying);
                yield return new WaitForSeconds(Random.Range(1.0f, 2.0f));
                StartCoroutine(SphereSpeech.Talk());
                ConvoSubtitle.text = SphereSpeech.gameObject.name + ": " + messageText;
                if(!convoCompleted)
                {

                    if (isPlayerActive)
                    {
                        if (firstTime)
                        {
                            StartCoroutine(PlayerConvo("Ah!, nice to meet you zack, we were talking about " + last_cube_convo, CubeSpeech.gameObject.name));
                            firstTime = false;
                        }
                        else
                        {
                            int rand = Random.Range(1, 3);
                            if (rand == 1)
                            {
                                StartCoroutine(CubeConvo(last_sphere_convo, CubeSpeech.gameObject.name));
                            }
                            else
                            {
                                StartCoroutine(PlayerConvo(last_sphere_convo, CubeSpeech.gameObject.name));
                            }
                        }
                    }
                    else
                    {
                        StartCoroutine(CubeConvo(last_sphere_convo, CubeSpeech.gameObject.name));
                    }
                }
            }
            if (responseData.emotion != null)
            {
                string behavior = responseData.emotion.behavior;
                string strength = responseData.emotion.strength;
            }
        }
        number_of_exchanges++;
    }

    //Function which controls the conversation of the Player.
    IEnumerator PlayerConvo(string last_convo, string askedBy)
    {
        //exactly same to the above functionality, but just changes the character.
        string apiURL = $"https://studio.inworld.ai/v1/workspaces/{workspaceId}/characters/{Character_name_from_InWorldAi}:simpleSendText";
        RequestData requestData = new RequestData
        {
            character = $"workspaces/{workspaceId}/characters/{Character_name_from_InWorldAi}",
            text = last_convo,
            endUserFullname = askedBy,
            endUserId = "12345"
        };

        string requestDataJson = JsonUtility.ToJson(requestData);

        UnityWebRequest request = new UnityWebRequest(apiURL, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestDataJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("authorization", "Basic " + apiKey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("POST request error: " + request.error);
        }
        else
        {
            ResponseData responseData = JsonUtility.FromJson<ResponseData>(request.downloadHandler.text);

            // Access the message text
            if (responseData.textList != null && responseData.textList.Length > 0)
            {
                string messageText = "";
                foreach (string message in responseData.textList)
                {
                    messageText += " " + message;
                }
                
                if (number_of_exchanges > TotalConversationExchange)
                {
                    messageText = "Well. its decided then, see you guys later!";
                    convoCompleted = true;
                }
                last_player_convo = messageText;
                PlayerSpeech.dialogue = messageText;
                CubeSpeech._handler = null;
                SphereSpeech._handler = null;
                StartCoroutine(PlayerSpeech.Prefetch());
                yield return new WaitUntil(() => !PlayerSpeech.isFetching && !CubeSpeech._audioSource.isPlaying && !SphereSpeech._audioSource.isPlaying);
                yield return new WaitForSeconds(Random.Range(1.0f, 2.0f));
                StartCoroutine(PlayerSpeech.Talk());
                ConvoSubtitle.text ="Player: " + messageText;
                if(!convoCompleted)
                {
                    int rand = Random.Range(1, 3);
                    if (rand == 1)
                    {
                        StartCoroutine(CubeConvo(last_player_convo, PlayerSpeech.gameObject.name));
                    }
                    else
                    {
                        StartCoroutine(SphereConvo(last_player_convo, PlayerSpeech.gameObject.name));
                    }
                }

                
            }
            if (responseData.emotion != null)
            {
                string behavior = responseData.emotion.behavior;
                string strength = responseData.emotion.strength;
            }
        }
        number_of_exchanges++;
    }
}
