import { useState } from "react";
import DbManagementTab from "./tabs/DbManagementTab";
import AnalysisTab from "./tabs/AnalysisTab";
import MethodologyTab from "./tabs/MethodologyTab";

const TABS = [
  {
    id: "methodology",
    label: "산출 방법론",
    icon: (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
      </svg>
    ),
    desc: "방법론 설명 및 참고문헌",
  },
  {
    id: "analysis",
    label: "일상섭취량 분석",
    icon: (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>
      </svg>
    ),
    desc: "NCI / ISU / MSM 분석 실행",
  },
  {
    id: "db",
    label: "DB 조회/관리",
    icon: (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/>
        <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/>
      </svg>
    ),
    desc: "자료 등록 및 식품군 관리",
  },
];

export default function App() {
  const [activeTab, setActiveTab] = useState("methodology");

  return (
    <div style={{ height: "100vh", display: "flex", flexDirection: "row", overflow: "hidden", background: "#f0f4f8" }}>

      {/* ── 사이드바 ── */}
      <aside style={{
        width: 210,
        flexShrink: 0,
        background: "linear-gradient(180deg, #1a2a4a 0%, #1e3460 60%, #243d72 100%)",
        display: "flex",
        flexDirection: "column",
        boxShadow: "2px 0 12px rgba(0,0,0,0.25)",
        zIndex: 10,
      }}>

        {/* 로고 영역 */}
        <div style={{
          padding: "20px 18px 16px",
          borderBottom: "1px solid rgba(255,255,255,0.08)",
          flexShrink: 0,
        }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 6 }}>
            <div style={{
              width: 34, height: 34,
              background: "linear-gradient(135deg, #4a90d9, #7ab8f5)",
              borderRadius: 9,
              display: "flex", alignItems: "center", justifyContent: "center",
              fontSize: 17, fontWeight: 800, color: "#fff", flexShrink: 0,
            }}>N</div>
            <div>
              <div style={{ fontSize: 13, fontWeight: 700, color: "#fff", lineHeight: 1.3 }}>일상섭취량</div>
              <div style={{ fontSize: 13, fontWeight: 700, color: "#fff", lineHeight: 1.3 }}>분석 프로그램</div>
            </div>
          </div>
          <div style={{
            fontSize: 10, color: "#7a9cc0",
            background: "rgba(255,255,255,0.06)",
            borderRadius: 4, padding: "3px 7px", display: "inline-block",
          }}>
            NCI · ISU · MSM
          </div>
        </div>

        {/* 네비게이션 */}
        <nav style={{ flex: 1, padding: "10px 8px", display: "flex", flexDirection: "column", gap: 2 }}>
          {TABS.map((tab) => {
            const active = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                style={{
                  display: "flex",
                  alignItems: "flex-start",
                  gap: 10,
                  padding: "10px 12px",
                  borderRadius: 7,
                  border: "none",
                  background: active
                    ? "linear-gradient(90deg, rgba(74,144,217,0.30) 0%, rgba(74,144,217,0.12) 100%)"
                    : "transparent",
                  borderLeft: active ? "3px solid #4a90d9" : "3px solid transparent",
                  color: active ? "#fff" : "#8ab0d0",
                  cursor: "pointer",
                  textAlign: "left",
                  fontFamily: "inherit",
                  transition: "all 0.15s",
                  width: "100%",
                }}
                onMouseEnter={e => { if (!active) e.currentTarget.style.background = "rgba(255,255,255,0.06)"; }}
                onMouseLeave={e => { if (!active) e.currentTarget.style.background = "transparent"; }}
              >
                <div style={{ marginTop: 1, flexShrink: 0, opacity: active ? 1 : 0.7 }}>
                  {tab.icon}
                </div>
                <div>
                  <div style={{ fontSize: 13, fontWeight: active ? 700 : 500, lineHeight: 1.4 }}>
                    {tab.label}
                  </div>
                  <div style={{ fontSize: 10, color: active ? "#a0c4e8" : "#4d6a88", marginTop: 2, lineHeight: 1.4 }}>
                    {tab.desc}
                  </div>
                </div>
              </button>
            );
          })}
        </nav>

        {/* 하단 버전 */}
        <div style={{
          padding: "12px 18px",
          borderTop: "1px solid rgba(255,255,255,0.07)",
          fontSize: 11,
          color: "#4d6a88",
          flexShrink: 0,
        }}>
          v2.4.1
        </div>
      </aside>

      {/* ── 콘텐츠 영역 ── */}
      <main style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", minHeight: 0, minWidth: 0 }}>

        {/* 콘텐츠 상단 헤더 바 */}
        <div style={{
          height: 50,
          background: "#fff",
          borderBottom: "1px solid #e2e8f0",
          display: "flex",
          alignItems: "center",
          padding: "0 24px",
          gap: 10,
          flexShrink: 0,
          boxShadow: "0 1px 4px rgba(0,0,0,0.04)",
        }}>
          <div style={{ color: "#4a90d9", flexShrink: 0 }}>
            {TABS.find(t => t.id === activeTab)?.icon}
          </div>
          <div>
            <div style={{ fontSize: 14, fontWeight: 700, color: "#1e293b", lineHeight: 1 }}>
              {TABS.find(t => t.id === activeTab)?.label}
            </div>
            <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2, lineHeight: 1 }}>
              {TABS.find(t => t.id === activeTab)?.desc}
            </div>
          </div>
        </div>

        {/* 탭 콘텐츠 */}
        <div style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", minHeight: 0 }}>
          {activeTab === "db"          && <DbManagementTab />}
          {activeTab === "analysis"    && <AnalysisTab />}
          {activeTab === "methodology" && <MethodologyTab onGoToAnalysis={() => setActiveTab("analysis")} />}
        </div>
      </main>
    </div>
  );
}
