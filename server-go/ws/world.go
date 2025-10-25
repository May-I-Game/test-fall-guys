package ws

import (
	"encoding/json"
	"server-go/protocol"
	"sync"
)

// World는 접속한 각 클라이언트의 최신 좌표를 보관합니다.
type World struct {
	mu      sync.RWMutex
	players map[string]protocol.Position
}

func NewWorld() *World {
	return &World{players: make(map[string]protocol.Position)}
}

func (w *World) UpdatePlayerPos(id string, pos protocol.Position) {
	w.mu.Lock()
	w.players[id] = pos
	w.mu.Unlock()
}

func (w *World) RemovePlayer(id string) {
	w.mu.Lock()
	delete(w.players, id)
	w.mu.Unlock()
}

type SnapshotPlayer struct {
	ID string  `json:"id"`
	X  float64 `json:"x"`
	Y  float64 `json:"y"`
	Z  float64 `json:"z"`
}

type Snapshot struct {
	Players []SnapshotPlayer `json:"players"`
}

func (w *World) Snapshot() Snapshot {
	w.mu.RLock()
	defer w.mu.RUnlock()
	out := Snapshot{Players: make([]SnapshotPlayer, 0, len(w.players))}
	for id, p := range w.players {
		out.Players = append(out.Players, SnapshotPlayer{ID: id, X: p.X, Y: p.Y, Z: p.Z})
	}
	return out
}

func (w *World) SnapshotJSON() []byte {
	s := w.Snapshot()
	b, _ := json.Marshal(s)
	return b
}
