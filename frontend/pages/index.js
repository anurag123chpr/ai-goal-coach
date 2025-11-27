import Head from 'next/head'
import { useState, useEffect } from 'react'
import GoalInput from '../components/GoalInput'
import GoalList from '../components/GoalList'

export default function Home() {
  const [saved, setSaved] = useState([])
  const [loading, setLoading] = useState(true)

  const fetchSaved = async () => {
    try {
      setLoading(true)
      const r = await fetch('http://localhost:8000/api/goals/list')
      const j = await r.json()
      if (j.ok) setSaved(j.items || [])
    } catch (e) {
      console.error('Could not fetch saved goals', e)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchSaved()
  }, [])

  return (
    <div className="container">
      <Head>
        <title>AI Goal Coach - Transform Vague Goals into SMART Goals</title>
        <meta name="description" content="Use AI to refine vague goals into SMART goals with measurable key results." />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </Head>

      <main>
        {/* Hero Section */}
        <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
          <h1>🎯 AI Goal Coach</h1>
          <p className="lead">
            Transform vague aspirations into actionable, measurable SMART goals with the power of AI.
          </p>
        </div>

        {/* Input Section */}
        <GoalInput onSaved={() => fetchSaved()} />

        {/* Divider */}
        <hr />

        {/* Saved Goals Section */}
        <div>
          <h2>📚 Saved Goals ({saved.length})</h2>
          {loading ? (
            <div style={{ textAlign: 'center', padding: '2rem' }}>
              <p style={{ color: 'var(--muted)' }}>Loading your goals...</p>
            </div>
          ) : (
            <GoalList items={saved} />
          )}
        </div>
      </main>
    </div>
  )
}
