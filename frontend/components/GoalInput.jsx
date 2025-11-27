import { useState } from 'react'

export default function GoalInput({ onSaved }) {
  const [text, setText] = useState('')
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState(null)
  const [error, setError] = useState(null)
  const [saved, setSaved] = useState(false)

  const handleRefine = async () => {
    setError(null)
    setResult(null)
    setSaved(false)
    if (!text || !text.trim()) {
      setError('Please enter a goal.')
      return
    }
    setLoading(true)
    try {
      const resp = await fetch('http://localhost:8000/api/goals/refine', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text })
      })
      const j = await resp.json()
      if (!j.ok) {
        setError('Could not refine the input. Try rephrasing.')
      } else {
        setResult(j.data)
      }
    } catch (e) {
      console.error(e)
      setError('Network error while calling backend.')
    } finally {
      setLoading(false)
    }
  }

  const handleSave = async () => {
    if (!result) return
    try {
      const resp = await fetch('http://localhost:8000/api/goals/save', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          refined_goal: result.refined_goal,
          key_results: result.key_results,
          confidence_score: result.confidence_score,
          source_text: text
        })
      })
      const j = await resp.json()
      if (j.ok) {
        setSaved(true)
        setText('')
        setResult(null)
        if (onSaved) onSaved()
        setTimeout(() => setSaved(false), 3000)
      } else {
        setError('Failed to save.')
      }
    } catch (e) {
      console.error(e)
      setError('Network error while saving.')
    }
  }

  return (
    <div className="card">
      <div className="card-header">
        <h2>Refine Your Goal</h2>
      </div>
      <p className="lead">Enter a vague goal and let AI help you create a SMART goal with measurable key results.</p>
      <textarea
        placeholder="e.g., I want to get better at sales..."
        value={text}
        onChange={(e) => setText(e.target.value)}
        rows={4}
      />
      <div className="controls">
        <button onClick={handleRefine} disabled={loading}>
          {loading ? (
            <>
              <span className="spinner"></span>
              Refining…
            </>
          ) : (
            'Refine Goal'
          )}
        </button>
        {result && (
          <button onClick={handleSave} className="secondary">
            Save Goal
          </button>
        )}
      </div>

      {error && <div className="error">{error}</div>}

      {saved && (
        <div className="success">✓ Goal saved successfully!</div>
      )}

      {result && (
        <div className="result">
          <h3>✨ Refined Goal</h3>
          <p style={{ fontSize: '1.1rem', fontWeight: 500, color: '#1f3a93' }}>
            {result.refined_goal}
          </p>

          <h4>📊 Key Results</h4>
          <ul>
            {result.key_results.map((kr, idx) => (
              <li key={idx}>{kr}</li>
            ))}
          </ul>

          <div className="confidence-score">
            <span>Confidence Score:</span>
            <span className="confidence-badge">{result.confidence_score}/10</span>
          </div>
        </div>
      )}
    </div>
  )
}
