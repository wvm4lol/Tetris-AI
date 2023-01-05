using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public class Piece : MonoBehaviour
{
    public TetrominoData data;
    public Tilemap tilemap;
    public Vector2Int[] blocks { get; private set; } //list of blocks of piece relative to center
    public Vector2Int startPosition; //position at top of board to spawn piece
    public Vector2Int position;
    public Vector2Int fieldOffset; //offset from center of tilemap to bottom left of field (tilemap:(fieldOffset) = field[0,0]
    public int rotationIndex; //rotation as int: 0 = normal, 1 = 90 degrees right, 2 = 180 degrees. 3 = 90 degrees left
    public Player player;
    public playerInput playerInput;

    public float maxLockTime; //max time given before locking a falling piece in place
    public bool lockPiece; //bool for saving when to lock piece at next autofall
    public float lockPieceTimer; //saved time when next drop should lock piece
    public float autoFallTime; //time between auto falls
    public float lastFallTime; //time when piece last autofell
    public float lastMoveTime;  //time when piece was last moved by input
    public float timeMultiplier;

    public int nextRotation; //0 = none, 1 = right, -1 = left
    public int nextMovement; //0 = none, 1 = right, -1 = left
    public int nextDrop; //0 = none, 1 = hard drop, -1 = soft drop
    public bool softDropping; //if piece is being soft dropped
    public float softDropMultiplier; 
    public int nextHold; //0 = none, 1 = hold piece
    public bool previousHold; //0 = not this piece, 1 = yes this piece. locks hold until piece placed
    float currTime; //time saved at start of loop

    bool active; //if class is active
    public bool gameOver; //if current game should be stopped
    RectInt bounds;


    public void Update()
    {

        if (!active || gameOver) //if the piece has not been initialized yet, do not execute update func
        {
            return;
        }


        ClearPiece(this);

        currTime = Time.time;

        UpdateFallSpeed();


        if (lockPiece == true)
        {
            //different loop when next piece drop should lock
            UnlockPiece();

            if (maxLockTime < currTime - lockPieceTimer)
            {
                StopClass();
                PlacePiece();
                return;
            }
            
        }
        
        //normal game loop
        if (nextHold == 1 && !previousHold)
        {
            StopClass();
            player.HoldPiece();
            previousHold = true;
            nextHold = 0;
            nextDrop = 0;
            nextRotation = 0;
            nextMovement = 0;
            ResumeClass();
        }

        if (nextDrop == 1)
        {
            HardDropPiece();
            nextDrop = 0;
            nextRotation = 0;
            nextMovement = 0;
        }

        if (nextDrop == -1 && softDropping == false)
        {
            softDropping = true;
        }

        if (nextDrop == 0 && softDropping == true)
        {
            softDropping = false;
        }

        if (nextRotation != 0)
        {
            RotatePiece(this);
            nextRotation = 0;
            nextMovement = 0;
        }

        if (nextMovement != 0)
        {
            MovePiece(this);
            nextMovement = 0;
        }

        if (PieceAutoFallTimer(this) && !lockPiece)
        {
            lastFallTime = Time.time;
            MovePieceDown(this);

        }

        if (!lockPiece)
        {
            LockPiece();
        }


        SetPiece(this);

    }

    public void LoadFirstPiece(TetrominoData tetrominoData, Vector2Int _position)
    {
        //Initialize class
        data = tetrominoData;
        blocks = new Vector2Int[data.blocks.Length];
        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i] = (Vector2Int)data.blocks[i];
        }
        startPosition = _position;
        position = startPosition + data.startPositionOffset;
        bounds = player.bounds;
        lastMoveTime = Time.time;
        rotationIndex = 0;
        fieldOffset= player.fieldOffset;
        ResumeClass();

    }

    public void LoadNextPiece(TetrominoData tetrominoData)
    {
        data = tetrominoData;
        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i] = (Vector2Int)data.blocks[i];
        }
        rotationIndex = 0;

        Vector2Int startOffset = new Vector2Int(0, 0);
        while (!IsValidLocation(this, startPosition + startOffset))
        {
            startOffset += new Vector2Int(0, 1);
        }

        position = startPosition + startOffset;
        lastMoveTime = Time.time;
        ResumeClass();
    }

    public void StopClass()
    {
        //stop class from executing update func by setting data to null
        active = false;
    }

    public void ResumeClass()
    {
        //resume class, inverse of stopclass func
        active = true;
    }

    public void SetPiece(Piece piece)
    {
        Vector2Int tilePositions = piece.position;

        //set all tiles on the tilemap containing the piece to correct color tile
        foreach (Vector2Int block in piece.blocks)
        {
            Vector3Int blockPosition = new Vector3Int(block.x + tilePositions.x, block.y + tilePositions.y, 0);
            tilemap.SetTile(blockPosition, piece.data.tile);

            try
            {
                player.field[blockPosition.x - fieldOffset.x, blockPosition.y - fieldOffset.y] = 1;
            }
            catch (Exception e)
            {
                print(e.ToString());
            }
        }
    }

    public void ClearPiece(Piece piece)
    {
        Vector2Int tilePositions = piece.position;

        //set all tiles on tilemap containing the piece clear
        foreach (Vector2Int block in piece.blocks)
        {
            Vector3Int blockPosition = new Vector3Int(block.x + tilePositions.x, block.y + tilePositions.y, 0);
            tilemap.SetTile(blockPosition, null);

            try
            {
                player.field[blockPosition.x - fieldOffset.x, blockPosition.y - fieldOffset.y] = 0;
            }
            catch (Exception e)
            {
                print(e.ToString());
            }
        }
    }

    public void LockPiece()
    {
        if (!IsValidLocation(this, position + Vector2Int.down))
        {
            lockPiece = true;
            lockPieceTimer = currTime;
        }
    }

    public void PlacePiece()
    {
        //locking piece, clearing lines and loading next piece
        SetPiece(this);
        player.CheckForClearedLines();
        player.CheckGameOver();
        //resetting values
        lockPiece = false;
        previousHold = false;
        nextHold = 0;
        player.ReceiveGarbage();
        player.garbageRowsToAdd = 0;
        player.NextPiece(); //shuffles bags, loads next piece into piece class
    }

    public void UnlockPiece()
    {
        if (IsValidLocation(this, position + Vector2Int.down))
        {
            lockPiece = false;
        }
    }


    public void RotateTiles(Piece piece, int direction)
    {
        float[] matrix = Data.RotationMatrix;
 
        // Rotate all of the piece.blocks using the rotation matrix
        for (int i = 0; i < piece.blocks.Length; i++)
        {
            Vector2 cell = piece.blocks[i];

            int x, y;

            switch (data.tetromino)
            {
                case Tetromino.I:
                case Tetromino.O:
                    // "I" and "O" are rotated from an offset center point
                    cell.x -= 0.5f;
                    cell.y -= 0.5f;
                    x = Mathf.CeilToInt((cell.x * matrix[0] * direction) + (cell.y * matrix[1] * direction));
                    y = Mathf.CeilToInt((cell.x * matrix[2] * direction) + (cell.y * matrix[3] * direction));
                    break;

                default:
                    x = Mathf.RoundToInt((cell.x * matrix[0] * direction) + (cell.y * matrix[1] * direction));
                    y = Mathf.RoundToInt((cell.x * matrix[2] * direction) + (cell.y * matrix[3] * direction));
                    break;
            }

            piece.blocks[i] = new Vector2Int(x, y);
        }
    }

    public void RotatePiece(Piece piece)
    {
        //rotate tiles beforehand for hitboxes
        RotateTiles(piece, piece.nextRotation);


        //math to find the correct row in the table of wallkicks
        int kickIndex = piece.rotationIndex * 2;
        

        if (piece.nextRotation == -1)
        {
            kickIndex--;
        }

        if (kickIndex < 0)
        {
            kickIndex = 7;
        } else if (kickIndex == 8)
        {
            kickIndex = 0;
        }

        //checking all wallkick positions for a piece, if none are valid rotate piece back
        for (int i = 0; i < piece.data.wallKicks.GetLength(1); i++){
            if (IsValidLocation(piece, piece.position + piece.data.wallKicks[kickIndex, i]))
            {
                piece.position += piece.data.wallKicks[kickIndex, i];
                piece.rotationIndex += piece.nextRotation;

                if (rotationIndex == -1)
                {
                    rotationIndex = 3;
                }
                else if (rotationIndex == 4)
                {
                    rotationIndex = 0;
                }
                return;
            } 
        }
        RotateTiles(piece, -piece.nextRotation);

    }

    public void MovePiece(Piece piece)
    {
        Vector2Int direction = new Vector2Int(piece.nextMovement, 0);

        if (IsValidLocation(piece, piece.position + direction)) 
        {
            piece.position += direction;
        }
    }

    public void HardDropPiece()
    {
        //check places below cvurrent location until invalid, set to previous position and fully place piece
        for (int i = 0; i < 24; i++)
        {
            if (!IsValidLocation(this, position + i * Vector2Int.down))
            {
                position += (i - 1) * Vector2Int.down;
                PlacePiece();
                return;
            }
        }
    }

    public void UpdateFallSpeed()
    {
        if (softDropping)
        {
            autoFallTime = timeMultiplier / softDropMultiplier;
            maxLockTime = (timeMultiplier / softDropMultiplier) * 8;
        } else
        {
            autoFallTime = timeMultiplier;
            maxLockTime = timeMultiplier * 8;
        }
    }


    public bool IsValidLocation(Piece piece, Vector2Int checkPosition)
    {
        //checks tile validity for all blocks in piece
        foreach (Vector2Int block in piece.blocks)
        {
            Vector2Int tilePosition = block + checkPosition;

            if (!IsWithinBounds(tilePosition))
            {
                return false;  
            }

            if (!IsTileValid(tilePosition))
            {
                return false;
            }
        }
        return true;
    }

    public bool IsWithinBounds(Vector2Int tilePosition)
    {
        //check if all blocks are within bounds
        if (!bounds.Contains(tilePosition))
        {
            return false;
        }
        return true;
    }

    public bool IsTileValid(Vector2Int tilePosition)
    {
        //check if space is not already occupied by tile
        if (tilemap.HasTile((Vector3Int)tilePosition))
        {
            return false;
        }
        return true;
    }

    public bool PieceAutoFallTimer(Piece piece)
    {
        //check if piece should auto fall (true = should autofall)
        float lastFallTime = piece.lastFallTime;

        if (currTime - lastFallTime > autoFallTime){
            return true;
        }
        return false;
    }



    public bool PieceLockTimer(Piece piece)
    {
        //check if piece should lock movement (true = should lock piece)
        float lastMoveTime = piece.lastMoveTime;

        if (currTime - lastMoveTime > maxLockTime)
        {
            return true;
        }
        return false;

    }

    public void MovePieceDown(Piece piece)
    {
        piece.position += Vector2Int.down;
    }


}
