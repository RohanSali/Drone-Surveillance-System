from sqlalchemy import create_engine, Column, Integer, String, Float, ForeignKey
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker

Base = declarative_base()
engine = create_engine('postgresql://postgres:A149@localhost:5432/drone_db')  # Update with your credentials
Session = sessionmaker(bind=engine)

class Group(Base):
    __tablename__ = 'groups'
    id = Column(Integer, primary_key=True)
    region = Column(String)  # e.g., 'urban_zone'
    purpose = Column(String)  # e.g., 'casualty_detection'
    rl_model_instance = Column(String)  # Identifier for the RL model

class Drone(Base):
    __tablename__ = 'drones'
    id = Column(Integer, primary_key=True)
    group_id = Column(Integer, ForeignKey('groups.id'))
    location = Column(String)
    last_score = Column(Float)

class DataLog(Base):
    __tablename__ = 'data_logs'
    id = Column(Integer, primary_key=True)
    drone_id = Column(Integer, ForeignKey('drones.id'))
    image_url = Column(String)
    location = Column(String)
    score = Column(Float)

Base.metadata.create_all(engine)