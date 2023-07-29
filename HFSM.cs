using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sandbox.Utils;

public interface IState {}
public class Transition<TEvent> where TEvent: struct, Enum
{
    private readonly IState m_to;
    private readonly Func<bool> m_guard = () => true;
    private readonly TEvent? m_event = null;
	
    public Transition(IState to)
    {
        m_to = to ?? throw new ArgumentNullException(nameof(to));;
    }
    
    public Transition(IState to, TEvent? @event = null, Func<bool>? guard = null) : this(to)
    {
	    m_to = to ?? throw new ArgumentNullException(nameof(to));
	    m_guard = guard ?? m_guard;
	    m_event = @event;
    }

    public bool MatchConditions(TEvent? fsmEvent)
    {
        return m_guard() && m_event.Equals(fsmEvent);
    }
    public IState Destination()
    {
        return m_to;
    }
    public override string ToString()
    {
	    StringBuilder builder = new StringBuilder();

	    builder.Append( $"To: {m_to} " );
	    if ( m_event.HasValue )
	    {
		    builder.Append( $"Event: {m_event} " );
	    }
	    builder.Append( $"Guard Result: {m_guard()} " ); // Don't really know yet how we can have a more meaningful debug info about the guard
	    return builder.ToString();
    }
}
public class State<TName, TEvent> : IState where TEvent: struct, Enum where TName: Enum
{
    private static readonly Action? NoActivity = () => { };
    public State(TName name)
    {
        Name = name;
    }
    
    public State(TName name, State<TName, TEvent>? root = null, Action? onEnterAction = null, Action? onUpdateAction = null,
	    Action? onExitAction = null)
	    : this(name)
    {
	    if (onEnterAction != null) m_onEnterAction = onEnterAction;
	    if (onUpdateAction != null) m_onUpdateAction = onUpdateAction;
	    if (onExitAction != null) m_onExitAction = onExitAction;
	    
	    m_parentState = root;
	    root?.AddChild(this);
    }

    public State(TName name, Action? onEnterAction = null, Action? onUpdateAction = null,
	    Action? onExitAction = null)
	    : this(name)
    {
	    if (onEnterAction != null) m_onEnterAction = onEnterAction;
	    if (onUpdateAction != null) m_onUpdateAction = onUpdateAction;
	    if (onExitAction != null) m_onExitAction = onExitAction;
    }
	

    public TName Name { get; private set; }

    private readonly Action? m_onEnterAction = NoActivity;
    private readonly Action? m_onUpdateAction = NoActivity;
    private readonly Action? m_onExitAction = NoActivity;

    private State<TName, TEvent>? m_parentState = null;
    private List<State<TName, TEvent>> m_children = new();
    private List<Transition<TEvent>> m_transitions = new();

    public void AddTransition(Transition<TEvent> transition)
    {
        m_transitions.Add(transition);
    }
    public State<TName, TEvent>? CheckTransitions(TEvent? fsmEvent, out Transition<TEvent>? matchedTransition )
    {
	    matchedTransition = null;
        foreach (Transition<TEvent> transition in m_transitions)
        {
	        if ( transition.MatchConditions( fsmEvent ) )
	        {
		        matchedTransition = transition;
		        return transition.Destination() as State<TName, TEvent>;
	        }
        }
        return null;
    }
    
    public List<State<TName, TEvent>> GetChildren()
    {
        return m_children;
    }
    private void AddChild(State<TName, TEvent> state) { m_children.Add(state); }
    
    public void SetParent(State<TName,TEvent> state)
    {
        m_parentState = state;
        m_parentState.AddChild(this);
    }
    
    public State<TName, TEvent>? GetParent()
    {
        return m_parentState;
    }

    public void OnEnter() { m_onEnterAction?.Invoke(); }
    public void OnUpdate() { m_onUpdateAction?.Invoke(); }
    public void OnExit() { m_onExitAction?.Invoke(); }

    public override string ToString()
    {
	    return $"[{Name}]";
    }
}

public class HFSMBuilder<TName, TEvent> where TEvent : struct, Enum where TName : Enum
{
    private readonly Dictionary<TName, State<TName, TEvent>> m_states = new();
    private readonly List<TransitionInfo> m_transitionInfos = new();
    private readonly Dictionary<TName, TName> m_stateParents = new();

    private class TransitionInfo
    {
        public TName From { get; }

        public TName To { get; }

        public Func<bool>? Guard { get; }

        public TEvent? Trigger { get; } = null;
		
        public TransitionInfo(TName from, TName to, Func<bool>? guard = null, TEvent? @event = null)
        {
            From = from;
            To = to;
            Guard = guard;
            Trigger = @event;
        }
        
        public TransitionInfo(TName from, TName to, TEvent @event)
        {
            From = from;
            To = to;
            Trigger = @event;
        }
    }
    
    public HFSMBuilder<TName, TEvent> AddState(TName name, Action? onEnterAction = null, Action? onUpdateAction = null, Action? onExitAction = null)
    {
        m_stateParents.Add(name, name);
        m_states.Add(name, new State<TName, TEvent>(name, onEnterAction, onUpdateAction, onExitAction));
        return this;
    }
    
    public HFSMBuilder<TName, TEvent> AddState(TName name, TName parentName, Action? onEnterAction = null, Action? onUpdateAction = null, Action? onExitAction = null)
    {
        if ( m_states.ContainsKey( name ) )
        {
	        throw new ArgumentException( $"State already defined {name}" );
        }
        
        m_stateParents.Add(name, parentName);
        m_states.Add(name, new State<TName, TEvent>(name, onEnterAction, onUpdateAction, onExitAction));
        return this;
    }
    
    public HFSMBuilder<TName, TEvent> AddTransition(TName from, TName to, TEvent? @event = null)
    {
        m_transitionInfos.Add(new TransitionInfo(from, to,null, @event));
        return this;
    }
	
    public HFSMBuilder<TName, TEvent> AddTransition(TName from, TName to, Func<bool> guard, TEvent? @event = null)
    {
        m_transitionInfos.Add(new TransitionInfo(from, to, guard, @event));
        return this;
    }

    public HFSM<TName, TEvent> Build()
    {
        State<TName, TEvent>? initial = null;
        
        foreach (var (name,state) in m_states)
        {
            if (!Equals(m_stateParents[name], name))
            {
                state.SetParent(m_states[m_stateParents[name]]);
            }
            else if (initial == null)
            {
                initial = state;
            }
        }
        
        foreach (var transitionInfo in m_transitionInfos)
        {
            m_states.TryGetValue( transitionInfo.From, out State<TName, TEvent>? from );
            m_states.TryGetValue( transitionInfo.To, out State<TName, TEvent>? to );

            if ( from == null )
            {
	            Log.Error( $"Cannot add transition {transitionInfo.From} => {transitionInfo.To} - {transitionInfo.From} state is not defined" );
	            continue;
            }
            
            if ( to == null )
            {
	            Log.Error( $"Cannot add transition {transitionInfo.From} => {transitionInfo.To} - {transitionInfo.To} state is not defined" );
	            continue;
            }
            
            from.AddTransition(new Transition<TEvent>(to, transitionInfo.Trigger, transitionInfo.Guard));
        }
        return new HFSM<TName, TEvent>(initial ?? throw new InvalidOperationException("No root state detected !"));
    }
}
public class HFSM<TName, TEvent> where TEvent: struct, Enum where TName: Enum 
{
    private readonly State<TName, TEvent> m_initialState;
    private State<TName, TEvent>? m_currentState;

    private readonly Queue<TEvent?> m_eventToTreat = new();
    
    public bool EnableDebugLog { set; get; } = false;
    
    private Transition<TEvent>? m_lastTransition;

    private void LogTransition(State<TName, TEvent>? oldState, State<TName, TEvent>? newState)
    {
	    if (EnableDebugLog)
	    {
		    Log.Info($"[HFSM] Transition: {oldState} -> {newState}, Reason: {m_lastTransition}");
	    }
    }
    
    public string GetDebugCurrentStateName()
    {
	    StringBuilder stateNamesBuilder = new StringBuilder();

	    foreach (var state in GetActiveStatesHierarchy())
	    {
		    stateNamesBuilder.Append('.');
		    stateNamesBuilder.Append(state.Name);
	    }
 
	    string names = stateNamesBuilder.ToString();

	    return names.StartsWith(".") ? names.TrimStart('.') : names;
    }

    public HFSM(State<TName, TEvent> initial)
    {
        m_initialState = initial;
    }
    
    public List<State<TName, TEvent>> GetActiveStatesHierarchy()
    {
        return m_currentState == null ? new List<State<TName, TEvent>>() : GetStatesHierarchy(m_currentState);
    }

    private List<State<TName, TEvent>> GetStatesHierarchy(State<TName, TEvent> root)
    {
        List<State<TName, TEvent>> activeHierarchy = new List<State<TName, TEvent>>();
        
        for (State<TName, TEvent>? parent = root; parent != null; parent = parent.GetParent())
        {
            activeHierarchy.Insert(0, parent);
        }
        
        return activeHierarchy;
    }

    private static State<TName, TEvent> GetFirstLeaf(State<TName, TEvent> state)
    {
        State<TName, TEvent> returnValue = state;
        while (returnValue.GetChildren().Count > 0)
        {
            returnValue = returnValue.GetChildren().First();
        }
        return returnValue;
    }
   
    public void Start()
    {
        ChangeActiveState(m_initialState);
    }
    
    public void Update()
    {
        if (m_currentState == null)
        {
            Start();
            return;
        }

        m_eventToTreat.TryDequeue(out var receivedEvent);
        
        foreach (State<TName, TEvent> state in GetActiveStatesHierarchy())
        {
            State<TName, TEvent>? destinationState = state.CheckTransitions(receivedEvent, out m_lastTransition);
            if (destinationState != null)
            {
	            ChangeActiveState(destinationState);
                break;
            }
            state.OnUpdate();
        }
    }
    
    public void SendEvent(TEvent trigger)
    {
        m_eventToTreat.Enqueue(trigger);
    }

    private void ChangeActiveState(State<TName, TEvent>? newState)
    {
        State<TName, TEvent>? nextState = newState != null ? GetFirstLeaf(newState) : null;
        
        HashSet<State<TName, TEvent>> activeStatesHierarchy = new HashSet<State<TName, TEvent>>(GetActiveStatesHierarchy());
        HashSet<State<TName, TEvent>> futureStatesHierarchy = nextState != null ? new HashSet<State<TName, TEvent>>(GetStatesHierarchy(nextState)) : new ();
        
        List<State<TName, TEvent>> exitingStates = activeStatesHierarchy.Except(futureStatesHierarchy).ToList();
        for (int index = exitingStates.Count - 1; index >= 0; --index)
        {
            exitingStates[index].OnExit();
        }
        
        LogTransition( m_currentState, nextState );
        m_currentState = nextState;
        
        List<State<TName, TEvent>> enteringStates = futureStatesHierarchy.Except(activeStatesHierarchy).ToList();
        foreach (State<TName, TEvent> state in enteringStates)
        {
            state.OnEnter();
        }
    }
    public void Stop()
    {
	    m_lastTransition = null;
        ChangeActiveState(null);
    }
    
    ~HFSM()
    {
	    if(m_currentState != null)
			Stop();
    }
}